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
        var increment = request.IncrementMinutes ?? 15;

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

            // Bu gün için aday başlangıçlar (grid).
            var cands = new List<(DateTime T, int? Cap, List<Guid> Instr)>();
            for (var t = dayWorkStart; t.AddMinutes(duration) <= dayWorkEnd; t = t.AddMinutes(increment))
            {
                var qe = t.AddMinutes(duration);
                if (date == today && t < nowLocal) continue;                                   // geçmiş başlangıç
                if (closures.Any(c => c.StartAt < qe && t < c.EndAt)) continue;                 // kapanış/tatil

                int? availCap = null;
                if (poolTotal is { } total)
                {
                    var peak = PeakUsed(poolSessions, t, qe);
                    var avail = total - peak;
                    if (avail < required) continue;                                            // ekipman yetersiz
                    availCap = avail;
                }

                var freeInstr = instructors.Where(i => IsFree(busyByInstructor, i.Id, t, qe)).Select(i => i.Id).ToList();
                if (freeInstr.Count == 0) continue;                                             // uygun hoca yok

                cands.Add((t, availCap, freeInstr));
            }

            MergeWindows(cands, date, duration, required, increment, nameById, result);
        }

        return result;
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

    /// <summary>Bitişik (increment kadar) aday başlangıçları, AYNI (kapasite + hoca kümesi) anahtarına göre tek
    /// pencerede birleştirir. Anahtar değişince veya boşlukta yeni pencere açar.</summary>
    private static void MergeWindows(
        List<(DateTime T, int? Cap, List<Guid> Instr)> cands, DateOnly date, int duration, int required, int increment,
        Dictionary<Guid, string> nameById, List<AvailabilityWindowDto> result)
    {
        static string Key(int? cap, List<Guid> instr) =>
            $"{(cap?.ToString() ?? "x")}|{string.Join(",", instr.OrderBy(x => x))}";

        var i = 0;
        while (i < cands.Count)
        {
            var start = cands[i];
            var key = Key(start.Cap, start.Instr);
            var lastT = start.T;
            var j = i + 1;
            while (j < cands.Count
                   && cands[j].T == lastT.AddMinutes(increment)
                   && Key(cands[j].Cap, cands[j].Instr) == key)
            {
                lastT = cands[j].T;
                j++;
            }

            var instrDtos = start.Instr
                .Select(id => new AvailabilityInstructorDto(id, nameById.TryGetValue(id, out var n) ? n : ""))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Add(new AvailabilityWindowDto(date, start.T, lastT, duration, required, start.Cap, instrDtos));
            i = j;
        }
    }
}
