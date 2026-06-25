using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Equipments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Availability;

/// <summary>
/// Sprint 32C: Boşluk bulma motoru. Seçilen ders tipi + süre + gerekli kapasite için, çalışma penceresi
/// (07:00–23:00) içinde dersin BAŞLATILABİLECEĞİ aralıkları (window-based) bulur. READ-ONLY (lock yok);
/// kesin doğrulama seans oluşturmada 32B guard'ı yapar. Ekipman kapasitesi yarı-açık [start,end) örtüşmesiyle,
/// kapasite zirvesi (peak) sweep-line ile hesaplanır → o aralıkta GARANTİ minimum boş kapasite.
/// </summary>
public sealed record FindClassTypeSlotsQuery(
    Guid ClassTypeId,
    DateOnly? Date,
    DateOnly? From,
    DateOnly? To,
    int DurationMinutes,
    int RequiredCapacity,
    Guid? InstructorProfileId,
    int? IncrementMinutes) : IQuery<IReadOnlyList<AvailabilityWindowDto>>;

public sealed class FindClassTypeSlotsValidator : AbstractValidator<FindClassTypeSlotsQuery>
{
    public FindClassTypeSlotsValidator()
    {
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.DurationMinutes).GreaterThan(0).LessThanOrEqualTo(600);
        RuleFor(x => x.RequiredCapacity).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.IncrementMinutes).GreaterThanOrEqualTo(5).When(x => x.IncrementMinutes.HasValue)
            .WithMessage("Adım (incrementMinutes) en az 5 dakika olmalıdır.");
    }
}

public sealed class FindClassTypeSlotsHandler : IQueryHandler<FindClassTypeSlotsQuery, IReadOnlyList<AvailabilityWindowDto>>
{
    private const int MaxRangeDays = 31;
    private static readonly TimeOnly WorkStart = new(7, 0);   // 07:00 (32C sabit; stüdyo WorkingHours ayarı post-MVP)
    private static readonly TimeOnly WorkEnd = new(23, 0);    // 23:00

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public FindClassTypeSlotsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AvailabilityWindowDto>> Handle(FindClassTypeSlotsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var duration = request.DurationMinutes;
        var required = request.RequiredCapacity;
        // Not: IncrementMinutes artık pencere SINIRLARINI belirlemez (eski sabit-ızgara modeli kaldırıldı).
        // Pencereler GERÇEK müsaitlik geçişlerinde (busy-edge) başlar/biter; istemci başlangıç seçeneklerini
        // pencere [from,to] aralığında kendi adımıyla üretir. Param geriye dönük uyum için kabul edilir.

        // ---- ClassType (tenant + aktif) ----
        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.ClassTypeId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders tipi", request.ClassTypeId);
        if (!classType.IsActive)
            throw new BusinessRuleException("Pasif ders tipi için boşluk aranamaz.");

        // ---- Opsiyonel hoca filtresi (aktif stüdyo) ----
        if (request.InstructorProfileId is { } instrFilter
            && !await _db.InstructorProfiles.AnyAsync(p => p.Id == instrFilter && p.StudioId == studioId, ct))
            throw new NotFoundException("Hoca profili", instrFilter);

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);
        var today = DateOnly.FromDateTime(nowLocal);

        // ---- Tarih aralığı: date (tek gün) | from/to | yoksa bugün ----
        DateOnly fromD, toD;
        if (request.Date is { } d) { fromD = d; toD = d; }
        else { fromD = request.From ?? today; toD = request.To ?? fromD; }
        if (toD < fromD)
            throw new BusinessRuleException("Bitiş tarihi başlangıçtan önce olamaz.");
        if (toD.DayNumber - fromD.DayNumber > MaxRangeDays)
            throw new BusinessRuleException($"Tarih aralığı en fazla {MaxRangeDays} gün olabilir.");

        // ---- Ekipman havuzu (varsa). Pasif/Quantity=0 → sonuç yok. ----
        int? poolTotal = null;
        Guid? poolId = classType.EquipmentId;
        if (poolId is { } eqId)
        {
            var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == eqId && e.StudioId == studioId, ct);
            if (equipment is null || !equipment.IsActive)
                return Array.Empty<AvailabilityWindowDto>(); // pasif/yok ekipman → boşluk yok
            poolTotal = EquipmentCapacityRules.TotalCapacity(equipment.Quantity, equipment.PerUnitConcurrentUsage);
        }

        var rangeStartDt = fromD.ToDateTime(TimeOnly.MinValue);
        var rangeEndDt = toD.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // ---- Toplu yükleme (range): Scheduled seanslar (hoca-meşgul + havuz kullanımı), pending telafiler, kapanışlar, hocalar ----
        var sessions = await _db.ClassSessions
            .Where(s => s.StudioId == studioId && s.Status == ClassSessionStatus.Scheduled
                        && s.StartAt < rangeEndDt && s.EndAt > rangeStartDt)
            .Select(s => new { s.InstructorProfileId, s.StartAt, s.EndAt, s.Capacity, EquipmentId = s.ClassType!.EquipmentId })
            .ToListAsync(ct);

        var pendings = await _db.MakeupRequests
            .Where(r => r.StudioId == studioId && r.Status == MakeupRequestStatus.Pending
                        && r.StartAt < rangeEndDt && r.EndAt > rangeStartDt)
            .Select(r => new { r.RequestedInstructorProfileId, r.StartAt, r.EndAt })
            .ToListAsync(ct);

        var closures = await _db.StudioClosures
            .Where(cl => cl.StudioId == studioId && cl.StartAt < rangeEndDt && cl.EndAt > rangeStartDt)
            .Select(cl => new { cl.StartAt, cl.EndAt })
            .ToListAsync(ct);

        var instructors = await _db.InstructorProfiles
            .Where(p => p.StudioId == studioId && (request.InstructorProfileId == null || p.Id == request.InstructorProfileId))
            .Select(p => new { p.Id, p.DisplayName })
            .ToListAsync(ct);
        var nameById = instructors.ToDictionary(i => i.Id, i => i.DisplayName);

        // Hoca-meşgul aralıkları: Scheduled seansları + pending telafileri (gerçek create de bunları bloklar).
        var busyByInstructor = sessions
            .GroupBy(s => s.InstructorProfileId)
            .ToDictionary(g => g.Key, g => g.Select(s => (s.StartAt, s.EndAt)).ToList());
        foreach (var p in pendings)
        {
            if (!busyByInstructor.TryGetValue(p.RequestedInstructorProfileId, out var list))
                busyByInstructor[p.RequestedInstructorProfileId] = list = new List<(DateTime, DateTime)>();
            list.Add((p.StartAt, p.EndAt));
        }

        // Havuz kullanımındaki seanslar (yalnız aynı EquipmentId + sonlu kapasite; 9999 hariç — 32B invariant).
        var poolSessions = poolId is { } pid
            ? sessions.Where(s => s.EquipmentId == pid && s.Capacity < EquipmentCapacityRules.UnlimitedCapacity)
                .Select(s => (s.StartAt, s.EndAt, s.Capacity)).ToList()
            : new List<(DateTime, DateTime, int)>();

        var result = new List<AvailabilityWindowDto>();

        for (var date = fromD; date <= toD; date = date.AddDays(1))
        {
            if (date < today) continue; // tamamı geçmiş gün → atla

            var dayWorkStart = date.ToDateTime(WorkStart, DateTimeKind.Unspecified);
            var dayWorkEnd = date.ToDateTime(WorkEnd, DateTimeKind.Unspecified);

            // Başlangıç-zamanı alanı [domain]: [en erken başlangıç, en geç başlangıç (workEnd − süre)].
            var domainStart = dayWorkStart;
            if (date == today && nowLocal > domainStart)
                domainStart = nowLocal.AddTicks(-(nowLocal.Ticks % TimeSpan.TicksPerMinute)); // saniyeyi at (temiz "şu an")
            var domainEnd = dayWorkEnd.AddMinutes(-duration);
            if (domainStart > domainEnd) continue; // güne 1 ders bile sığmıyor

            // ---- Olay noktaları: başlangıç ekseninde müsaitlik/kapasite/kapanış DEĞİŞTİĞİ anlar ----
            // Sabit ızgara YOK → pencereler GERÇEK geçişlerde başlar/biter. Bir [bs,be) meşgul aralığı, başlangıç t
            // için (bs−süre, be) boyunca çakışır; dolayısıyla geçiş noktaları (bs−süre) ve be'dir.
            var events = new SortedSet<DateTime> { domainStart, domainEnd };
            void AddEvent(DateTime t) { if (t > domainStart && t < domainEnd) events.Add(t); }

            foreach (var i in instructors)
                if (busyByInstructor.TryGetValue(i.Id, out var blocks))
                    foreach (var (bs, be) in blocks) { AddEvent(bs.AddMinutes(-duration)); AddEvent(be); }
            foreach (var c in closures) { AddEvent(c.StartAt.AddMinutes(-duration)); AddEvent(c.EndAt); }
            foreach (var p in poolSessions) { AddEvent(p.Item1.AddMinutes(-duration)); AddEvent(p.Item2); }

            // ---- Segmentler: ardışık olaylar arası. İÇ noktada (midpoint) değer sabit → aralık için GARANTİ. ----
            var ev = events.ToList();
            var daySegs = new List<(DateTime From, DateTime To, int? Cap, List<Guid> Instr)>();
            for (var k = 0; k + 1 < ev.Count; k++)
            {
                var a = ev[k];
                var b = ev[k + 1];
                if (b <= a) continue;
                var qs = a.AddMinutes((b - a).TotalMinutes / 2); // segment iç noktası
                var qe = qs.AddMinutes(duration);

                if (closures.Any(c => c.StartAt < qe && qs < c.EndAt)) continue; // kapanış → boşluk yok

                int? availCap = null;
                if (poolTotal is { } total)
                {
                    var avail = total - PeakUsed(poolSessions, qs, qe);
                    if (avail < required) continue; // ekipman yetersiz
                    availCap = avail;
                }

                var freeInstr = instructors
                    .Where(i => IsFree(busyByInstructor, i.Id, qs, qe))
                    .Select(i => i.Id).OrderBy(x => x).ToList();
                if (freeInstr.Count == 0) continue; // uygun hoca yok

                daySegs.Add((a, b, availCap, freeInstr));
            }

            MergeAdjacent(daySegs, date, duration, required, nameById, result);
        }

        return result;
    }

    /// <summary>Bitişik (To == sonraki From) ve AYNI (kapasite + hoca kümesi) segmentleri tek pencerede birleştirir;
    /// pencerenin From/To'su geçerli başlangıç aralığını (start-range) verir.</summary>
    private static void MergeAdjacent(
        List<(DateTime From, DateTime To, int? Cap, List<Guid> Instr)> segs, DateOnly date, int duration, int required,
        Dictionary<Guid, string> nameById, List<AvailabilityWindowDto> result)
    {
        static string Key(int? cap, List<Guid> instr) => $"{(cap?.ToString() ?? "x")}|{string.Join(",", instr)}";

        var i = 0;
        while (i < segs.Count)
        {
            var seg = segs[i];
            var key = Key(seg.Cap, seg.Instr);
            var to = seg.To;
            var j = i + 1;
            while (j < segs.Count && segs[j].From == to && Key(segs[j].Cap, segs[j].Instr) == key)
            {
                to = segs[j].To;
                j++;
            }

            var instrDtos = seg.Instr
                .Select(id => new AvailabilityInstructorDto(id, nameById.TryGetValue(id, out var n) ? n : ""))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Add(new AvailabilityWindowDto(date, seg.From, to, duration, required, seg.Cap, instrDtos));
            i = j;
        }
    }

    /// <summary>[qs,qe) aralığında ekipman kullanım ZİRVESİ (eşzamanlı en yüksek kapasite). Sweep: zirve yalnız bir
    /// seansın başlangıcında (veya pencere başında) artabileceğinden, bu noktalardaki seviyelerin en büyüğü alınır.</summary>
    private static int PeakUsed(List<(DateTime S, DateTime E, int Cap)> poolSessions, DateTime qs, DateTime qe)
    {
        if (poolSessions.Count == 0) return 0;
        var points = new List<DateTime> { qs };
        foreach (var p in poolSessions)
            if (p.S > qs && p.S < qe) points.Add(p.S);

        var peak = 0;
        foreach (var x in points)
        {
            var level = 0;
            foreach (var p in poolSessions)
                if (p.S <= x && x < p.E) level += p.Cap;   // yarı-açık: x=E aktif değil
            if (level > peak) peak = level;
        }
        return peak;
    }

    private static bool IsFree(Dictionary<Guid, List<(DateTime S, DateTime E)>> busy, Guid instrId, DateTime qs, DateTime qe)
        => !busy.TryGetValue(instrId, out var list) || !list.Any(b => b.S < qe && qs < b.E);
}
