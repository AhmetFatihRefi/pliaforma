using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Contracts;

// ======================= DTO'lar =======================
// Sprint 28C-1: grup düzeni değişikliği LINE-bazlı (seçim birimi = lineId).
/// <summary>Grup düzeni değişikliğine aday öğrenci/line (hoca + ders tipinde gelecek Regular dersi olan).</summary>
public sealed record GroupChangeCandidateDto(
    Guid LineId, Guid StudentProfileId, string StudentName,
    int AvailableLessonCount, DateOnly StartDate, DateOnly EndDate,
    int FutureRegularCount, string CurrentSlotSummary, bool IsIndividual);

/// <summary>Yeniden üretimde tek slotun sonucu. Outcome: created-new | reused | skipped | conflict.</summary>
public sealed record GroupChangeSlotOutcomeDto(DateTime StartAt, DateTime EndAt, string Outcome, string? Reason);

/// <summary>Bir line için grup-değişikliği sonucu (kaldırılan + üretilen + çakışma).</summary>
public sealed record GroupChangeStudentResultDto(
    Guid LineId, string StudentName,
    int RemovedCount, int PlannedCount, int ReusedCount, int NewSessionCount,
    int SkippedCount, int ConflictCount, string? Note,
    IReadOnlyList<GroupChangeSlotOutcomeDto> Slots);

/// <summary>Grup-değişikliği önizleme/sonuç: öğrenci-bazlı sonuçlar + boşalıp iptal edilen session sayısı + uygulanabilir mi.</summary>
public sealed record GroupChangePreviewDto(
    IReadOnlyList<GroupChangeStudentResultDto> Students, int EmptiedSessionsCancelled, bool CanApply);

// ======================= Ortak çekirdek =======================
/// <summary>
/// Köklü grup düzeni değişikliği çekirdeği (Sprint 22). Seçili sözleşmeler için: effectiveDate'ten itibaren
/// gelecek Booked Regular bookingleri TELAFİSİZ iptal eder; boşalan eski sessionları (başka öğrencisi
/// kalmayanları) telafisiz Cancelled yapar; sonra yeni slotlardan sözleşme-bazlı ders üretir (cross-student
/// reuse — sırayla işlenip her öğrenciden sonra SaveChanges ile bir sonraki öğrenci aynı yeni grup seansını
/// reuse eder). Telafi ÜRETMEZ (program düzeni değişikliği). Hoca/öğrenci çakışması → BLOK (canApply=false).
/// available aşımı / tarih dolması → best-effort (skipped). PREVIEW bu çekirdeği transaction'da çalıştırıp
/// ROLLBACK eder → preview ile apply BİREBİR aynı.
/// </summary>
internal sealed class GroupScheduleChangeService
{
    private static readonly string[] TrDays = { "Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt" };
    public static string SlotLabel(DateTime startAt) => $"{TrDays[(int)startAt.DayOfWeek]} {startAt:HH:mm}";

    public async Task<GroupChangePreviewDto> ExecuteAsync(
        IAppDbContext db, ICurrentUserService user, IDateTime clock,
        Guid instructorProfileId, Guid classTypeId, DateOnly effectiveDate,
        IReadOnlyList<Guid> lineIds, IReadOnlyList<ContractScheduleSlotInput> newSlots, CancellationToken ct)
    {
        var studioId = user.RequireStudioId();
        var userId = user.RequireUserId();
        if (user.StudioRole is not (StudioRole.Owner or StudioRole.StudioAdmin))
            throw new ForbiddenException("Grup program değişikliği yalnızca Owner/StudioAdmin yetkisindedir.");
        if (lineIds.Count == 0) throw new BusinessRuleException("En az bir öğrenci (satır) seçilmeli.");
        if (newSlots.Count == 0) throw new BusinessRuleException("En az bir yeni gün/saat seçilmeli.");

        var nowLocal = await SessionRules.StudioNowAsync(db, clock, studioId, ct);
        if (effectiveDate < DateOnly.FromDateTime(nowLocal))
            throw new BusinessRuleException("Geçerlilik tarihi (effective date) geçmiş olamaz.");
        var effectiveDt = effectiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        var classType = await db.ClassTypes.FirstOrDefaultAsync(c => c.Id == classTypeId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders tipi", classTypeId);
        var duration = classType.DefaultDurationMinutes;
        var closures = await db.StudioClosures.Where(cl => cl.StudioId == studioId).ToListAsync(ct);

        var lines = await db.StudentContractLines.Include(l => l.StudentContract)
            .Where(l => l.StudioId == studioId && lineIds.Contains(l.Id)).ToListAsync(ct);
        var ordered = new List<StudentContractLine>();
        foreach (var id in lineIds.Distinct())
        {
            var l = lines.FirstOrDefault(x => x.Id == id) ?? throw new NotFoundException("Sözleşme satırı", id);
            if (l.Status != StudentContractLineStatus.Active)
                throw new BusinessRuleException("Yalnızca aktif satır taşınabilir.");
            if (l.InstructorProfileId != instructorProfileId || l.ClassTypeId != classTypeId)
                throw new BusinessRuleException("Seçilen satır bu hoca/ders tipiyle uyuşmuyor.");
            ordered.Add(l);
        }

        var studentIds = ordered.Select(l => l.StudentContract!.StudentProfileId).Distinct().ToList();
        var names = await db.StudentProfiles.Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName, ct);

        // PHASE 1 — Seçili satırların effectiveDate ve sonrası Booked Regular bookinglerini TELAFİSİZ iptal.
        var removedByLine = new Dictionary<Guid, int>();
        var affectedSessionIds = new HashSet<Guid>();
        foreach (var l in ordered)
        {
            var toRemove = await db.ClassBookings
                .Where(b => b.StudioId == studioId && b.StudentContractLineId == l.Id && b.Status == ClassBookingStatus.Booked
                            && b.ClassSession!.SessionKind == ClassSessionKind.Regular
                            && b.ClassSession.Status == ClassSessionStatus.Scheduled
                            && b.ClassSession.StartAt >= effectiveDt)
                .ToListAsync(ct);
            foreach (var b in toRemove)
            {
                b.Status = ClassBookingStatus.CancelledByStudio;
                b.CancelledByUserId = userId;
                b.CancelledAt = clock.UtcNow;
                b.CancellationReason = "Grup program değişikliği nedeniyle taşındı";
                affectedSessionIds.Add(b.ClassSessionId);
            }
            removedByLine[l.Id] = toRemove.Count;
        }
        await db.SaveChangesAsync(ct);

        // PHASE 2 — Etkilenen sessionların BookedCount'ını güncelle; tamamen boşalanları TELAFİSİZ Cancelled yap.
        var emptied = 0;
        foreach (var sid in affectedSessionIds)
        {
            var remaining = await db.ClassBookings.CountAsync(b => b.ClassSessionId == sid && b.Status == ClassBookingStatus.Booked, ct);
            var sess = await db.ClassSessions.FirstOrDefaultAsync(s => s.Id == sid, ct);
            if (sess is null) continue;
            sess.BookedCount = remaining;
            if (remaining == 0 && sess.Status == ClassSessionStatus.Scheduled)
            {
                sess.Status = ClassSessionStatus.Cancelled;
                sess.Notes = "Grup program değişikliği — boş kaldı";
                emptied++;
            }
        }
        await db.SaveChangesAsync(ct);

        // PHASE 3 — Her satır için yeni slotlardan yeniden üret (sıralı; her satırdan sonra SaveChanges → reuse).
        // Adaylar line tarih aralığında (BuildCandidates(line)) → yeni dersler line.StartDate..EndDate içinde garanti.
        var results = new List<GroupChangeStudentResultDto>();
        var canApply = true;
        foreach (var l in ordered)
        {
            var studentProfileId = l.StudentContract!.StudentProfileId;
            var individual = l.IsIndividual;
            var available = await ContractLineRules.AvailableAsync(db, l, ct);
            var candidates = ContractLineScheduleEngine.BuildCandidates(l, duration, newSlots)
                .Where(x => x.StartAt >= effectiveDt).ToList();

            var slots = new List<GroupChangeSlotOutcomeDto>();
            int planned = 0, reused = 0, newS = 0, skipped = 0, conflicts = 0;
            foreach (var cand in candidates)
            {
                if (planned >= available) break; // best-effort: ders hakkı bitti
                if (cand.StartAt <= nowLocal) { skipped++; slots.Add(new(cand.StartAt, cand.EndAt, "skipped", "Geçmiş zaman")); continue; }
                if (ContractScheduleEngine.InAnyClosure(cand.StartAt, closures)) { skipped++; slots.Add(new(cand.StartAt, cand.EndAt, "skipped", "Stüdyo kapalı")); continue; }

                var target = individual ? null
                    : await ContractScheduleEngine.FindReusableGroupAsync(db, studioId, instructorProfileId, classTypeId, cand.StartAt, ct);

                if (target is null && await SessionRules.HasConflictAsync(db, studioId, instructorProfileId, cand.StartAt, cand.EndAt, null, ct))
                { conflicts++; canApply = false; slots.Add(new(cand.StartAt, cand.EndAt, "conflict", "Hoca çakışması")); continue; }

                if (await ContractScheduleEngine.StudentBusyAsync(db, studioId, studentProfileId, cand.StartAt, cand.EndAt, ct))
                { conflicts++; canApply = false; slots.Add(new(cand.StartAt, cand.EndAt, "conflict", "Öğrenci çakışması")); continue; }

                if (target is null)
                {
                    target = ContractScheduleEngine.NewSession(studioId, instructorProfileId, classTypeId, cand.StartAt, cand.EndAt, individual, userId);
                    db.ClassSessions.Add(target);
                    newS++;
                    slots.Add(new(cand.StartAt, cand.EndAt, "created-new", null));
                }
                else { reused++; slots.Add(new(cand.StartAt, cand.EndAt, "reused", null)); }

                target.BookedCount += 1;
                db.ClassBookings.Add(ContractLineScheduleEngine.NewBooking(l, target.Id, userId));
                planned++;
            }
            await db.SaveChangesAsync(ct); // cross-student reuse görünürlüğü

            var note = planned < available ? $"{available} ders hakkından {planned} ders üretilebildi" : null;
            results.Add(new GroupChangeStudentResultDto(
                l.Id, names.GetValueOrDefault(studentProfileId, ""), removedByLine.GetValueOrDefault(l.Id),
                planned, reused, newS, skipped, conflicts, note, slots));
        }

        return new GroupChangePreviewDto(results, emptied, canApply);
    }
}

// ======================= Adaylar (read-only) =======================
public sealed record GetGroupChangeCandidatesQuery(
    Guid InstructorProfileId, Guid ClassTypeId, DateOnly EffectiveDate, Domain.Enums.StudentLevel? Level = null)
    : IQuery<IReadOnlyList<GroupChangeCandidateDto>>;

public sealed class GetGroupChangeCandidatesHandler : IQueryHandler<GetGroupChangeCandidatesQuery, IReadOnlyList<GroupChangeCandidateDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetGroupChangeCandidatesHandler(IAppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<GroupChangeCandidateDto>> Handle(GetGroupChangeCandidatesQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var effectiveDt = request.EffectiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Sprint 28C-1: aday birimi = aktif LINE (hoca + ders tipi eşleşen). Opsiyonel seviye filtresi.
        var lines = await _db.StudentContractLines.Include(l => l.StudentContract)
            .Where(l => l.StudioId == studioId && l.InstructorProfileId == request.InstructorProfileId
                        && l.ClassTypeId == request.ClassTypeId && l.Status == StudentContractLineStatus.Active
                        && (request.Level == null || l.StudentContract!.StudentProfile!.Level == request.Level))
            .ToListAsync(ct);
        var lineIds = lines.Select(l => l.Id).ToList();
        if (lineIds.Count == 0) return Array.Empty<GroupChangeCandidateDto>();

        var future = await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.StudentContractLineId != null && lineIds.Contains(b.StudentContractLineId!.Value)
                        && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular
                        && b.ClassSession.Status == ClassSessionStatus.Scheduled && b.ClassSession.StartAt >= effectiveDt)
            .Select(b => new { LId = b.StudentContractLineId!.Value, b.ClassSession!.StartAt })
            .ToListAsync(ct);
        var byLine = future.GroupBy(x => x.LId).ToDictionary(g => g.Key, g => g.ToList());

        var studentIds = lines.Select(l => l.StudentContract!.StudentProfileId).Distinct().ToList();
        var names = await _db.StudentProfiles.Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName, ct);

        var result = new List<GroupChangeCandidateDto>();
        foreach (var l in lines)
        {
            if (!byLine.TryGetValue(l.Id, out var fut) || fut.Count == 0) continue; // yalnız gelecek Regular dersi olanlar
            var available = await ContractLineRules.AvailableAsync(_db, l, ct);
            var summary = string.Join(", ", fut.Select(x => GroupScheduleChangeService.SlotLabel(x.StartAt)).Distinct().OrderBy(s => s));
            result.Add(new GroupChangeCandidateDto(
                l.Id, l.StudentContract!.StudentProfileId, names.GetValueOrDefault(l.StudentContract!.StudentProfileId, ""),
                available, l.StartDate, l.EndDate, fut.Count, summary, l.IsIndividual));
        }
        return result.OrderBy(r => r.StudentName).ToList();
    }
}

// ======================= Önizleme (dry-run + rollback = apply ile birebir) =======================
public sealed record PreviewGroupScheduleChangeQuery(
    Guid InstructorProfileId, Guid ClassTypeId, DateOnly EffectiveDate,
    IReadOnlyList<Guid> LineIds, IReadOnlyList<ContractScheduleSlotInput> NewSlots) : IQuery<GroupChangePreviewDto>;

public sealed class PreviewGroupScheduleChangeHandler : IQueryHandler<PreviewGroupScheduleChangeQuery, GroupChangePreviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ITransactionManager _transactions;
    public PreviewGroupScheduleChangeHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, ITransactionManager transactions)
    { _db = db; _currentUser = currentUser; _clock = clock; _transactions = transactions; }

    public async Task<GroupChangePreviewDto> Handle(PreviewGroupScheduleChangeQuery request, CancellationToken ct)
    {
        // DRY-RUN: gerçek apply mantığını transaction içinde çalıştır, sonra ROLLBACK → preview = apply, kalıcı yazma yok.
        await using var tx = await _transactions.BeginTransactionAsync(ct);
        try
        {
            var result = await new GroupScheduleChangeService().ExecuteAsync(
                _db, _currentUser, _clock, request.InstructorProfileId, request.ClassTypeId, request.EffectiveDate,
                request.LineIds, request.NewSlots, ct);
            return result;
        }
        finally
        {
            await tx.RollbackAsync(ct);
        }
    }
}

// ======================= Uygula (atomik; telafi YOK) =======================
public sealed record ApplyGroupScheduleChangeCommand(
    Guid InstructorProfileId, Guid ClassTypeId, DateOnly EffectiveDate,
    IReadOnlyList<Guid> LineIds, IReadOnlyList<ContractScheduleSlotInput> NewSlots)
    : ICommand<GroupChangePreviewDto>, IStudioWriteRequest;

public sealed class ApplyGroupScheduleChangeHandler : ICommandHandler<ApplyGroupScheduleChangeCommand, GroupChangePreviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public ApplyGroupScheduleChangeHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<GroupChangePreviewDto> Handle(ApplyGroupScheduleChangeCommand request, CancellationToken ct)
    {
        var result = await new GroupScheduleChangeService().ExecuteAsync(
            _db, _currentUser, _clock, request.InstructorProfileId, request.ClassTypeId, request.EffectiveDate,
            request.LineIds, request.NewSlots, ct);

        // HARD çakışma varsa atomik abort: exception → TransactionBehavior tüm işlemi rollback eder (yarım taşıma yok).
        if (!result.CanApply)
            throw new ConflictException("Çakışma var; grup değişikliği uygulanamadı. Önizlemeyi kontrol edin (hoca/öğrenci çakışması).");

        return result;
    }
}
