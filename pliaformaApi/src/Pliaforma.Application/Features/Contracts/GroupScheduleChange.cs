using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Contracts;

// ======================= DTO'lar =======================
/// <summary>Grup düzeni değişikliğine aday öğrenci/sözleşme (hoca + ders tipinde gelecek Regular dersi olan).</summary>
public sealed record GroupChangeCandidateDto(
    Guid ContractId, Guid StudentProfileId, string StudentName,
    int AvailableLessonCount, DateOnly StartDate, DateOnly EndDate,
    int FutureRegularCount, string CurrentSlotSummary, bool IsIndividual);

/// <summary>Yeniden üretimde tek slotun sonucu. Outcome: created-new | reused | skipped | conflict.</summary>
public sealed record GroupChangeSlotOutcomeDto(DateTime StartAt, DateTime EndAt, string Outcome, string? Reason);

/// <summary>Bir öğrenci için grup-değişikliği sonucu (kaldırılan + üretilen + çakışma).</summary>
public sealed record GroupChangeStudentResultDto(
    Guid ContractId, string StudentName,
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
        IReadOnlyList<Guid> contractIds, IReadOnlyList<ContractScheduleSlotInput> newSlots, CancellationToken ct)
    {
        var studioId = user.RequireStudioId();
        var userId = user.RequireUserId();
        if (user.StudioRole is not (StudioRole.Owner or StudioRole.StudioAdmin))
            throw new ForbiddenException("Grup program değişikliği yalnızca Owner/StudioAdmin yetkisindedir.");
        if (contractIds.Count == 0) throw new BusinessRuleException("En az bir öğrenci seçilmeli.");
        if (newSlots.Count == 0) throw new BusinessRuleException("En az bir yeni gün/saat seçilmeli.");

        var nowLocal = await SessionRules.StudioNowAsync(db, clock, studioId, ct);
        if (effectiveDate < DateOnly.FromDateTime(nowLocal))
            throw new BusinessRuleException("Geçerlilik tarihi (effective date) geçmiş olamaz.");
        var effectiveDt = effectiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        var classType = await db.ClassTypes.FirstOrDefaultAsync(c => c.Id == classTypeId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders tipi", classTypeId);
        var duration = classType.DefaultDurationMinutes;
        var closures = await db.StudioClosures.Where(cl => cl.StudioId == studioId).ToListAsync(ct);

        var contracts = await db.StudentContracts
            .Where(c => c.StudioId == studioId && contractIds.Contains(c.Id)).ToListAsync(ct);
        var ordered = new List<StudentContract>();
        foreach (var id in contractIds.Distinct())
        {
            var c = contracts.FirstOrDefault(x => x.Id == id) ?? throw new NotFoundException("Sözleşme", id);
            if (c.Status != StudentContractStatus.Active)
                throw new BusinessRuleException("Yalnızca aktif sözleşme taşınabilir.");
            if (c.InstructorProfileId != instructorProfileId || c.ClassTypeId != classTypeId)
                throw new BusinessRuleException("Seçilen sözleşme bu hoca/ders tipiyle uyuşmuyor.");
            ordered.Add(c);
        }

        var studentIds = ordered.Select(c => c.StudentProfileId).Distinct().ToList();
        var names = await db.StudentProfiles.Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName, ct);

        // PHASE 1 — Seçili sözleşmelerin effectiveDate ve sonrası Booked Regular bookinglerini TELAFİSİZ iptal.
        var removedByContract = new Dictionary<Guid, int>();
        var affectedSessionIds = new HashSet<Guid>();
        foreach (var c in ordered)
        {
            var toRemove = await db.ClassBookings
                .Where(b => b.StudioId == studioId && b.StudentContractId == c.Id && b.Status == ClassBookingStatus.Booked
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
            removedByContract[c.Id] = toRemove.Count;
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

        // PHASE 3 — Her sözleşme için yeni slotlardan yeniden üret (sıralı; her öğrenciden sonra SaveChanges → reuse).
        var results = new List<GroupChangeStudentResultDto>();
        var canApply = true;
        foreach (var c in ordered)
        {
            var individual = c.IsIndividualDefault;
            var reserved = await ContractScheduleEngine.ReservedRegularAsync(db, c.Id, ct);
            var available = Math.Max(0, c.TotalLessonCount - reserved);
            var candidates = ContractScheduleEngine.BuildCandidates(c, duration, newSlots)
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

                if (await ContractScheduleEngine.StudentBusyAsync(db, studioId, c.StudentProfileId, cand.StartAt, cand.EndAt, ct))
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
                db.ClassBookings.Add(ContractScheduleEngine.NewBooking(studioId, target.Id, c.StudentProfileId, c.Id, userId));
                planned++;
            }
            await db.SaveChangesAsync(ct); // cross-student reuse görünürlüğü

            var note = planned < available ? $"{available} ders hakkından {planned} ders üretilebildi" : null;
            results.Add(new GroupChangeStudentResultDto(
                c.Id, names.GetValueOrDefault(c.StudentProfileId, ""), removedByContract.GetValueOrDefault(c.Id),
                planned, reused, newS, skipped, conflicts, note, slots));
        }

        return new GroupChangePreviewDto(results, emptied, canApply);
    }
}

// ======================= Adaylar (read-only) =======================
public sealed record GetGroupChangeCandidatesQuery(Guid InstructorProfileId, Guid ClassTypeId, DateOnly EffectiveDate)
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

        var contracts = await _db.StudentContracts
            .Where(c => c.StudioId == studioId && c.InstructorProfileId == request.InstructorProfileId
                        && c.ClassTypeId == request.ClassTypeId && c.Status == StudentContractStatus.Active)
            .ToListAsync(ct);
        var contractIds = contracts.Select(c => c.Id).ToList();
        if (contractIds.Count == 0) return Array.Empty<GroupChangeCandidateDto>();

        var future = await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.StudentContractId != null && contractIds.Contains(b.StudentContractId!.Value)
                        && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular
                        && b.ClassSession.Status == ClassSessionStatus.Scheduled && b.ClassSession.StartAt >= effectiveDt)
            .Select(b => new { CId = b.StudentContractId!.Value, b.ClassSession!.StartAt })
            .ToListAsync(ct);
        var byContract = future.GroupBy(x => x.CId).ToDictionary(g => g.Key, g => g.ToList());

        var studentIds = contracts.Select(c => c.StudentProfileId).Distinct().ToList();
        var names = await _db.StudentProfiles.Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName, ct);

        var result = new List<GroupChangeCandidateDto>();
        foreach (var c in contracts)
        {
            if (!byContract.TryGetValue(c.Id, out var fut) || fut.Count == 0) continue; // yalnız gelecek Regular dersi olanlar
            var reserved = await ContractScheduleEngine.ReservedRegularAsync(_db, c.Id, ct);
            var available = Math.Max(0, c.TotalLessonCount - reserved);
            var summary = string.Join(", ", fut.Select(x => GroupScheduleChangeService.SlotLabel(x.StartAt)).Distinct().OrderBy(s => s));
            result.Add(new GroupChangeCandidateDto(
                c.Id, c.StudentProfileId, names.GetValueOrDefault(c.StudentProfileId, ""),
                available, c.StartDate, c.EndDate, fut.Count, summary, c.IsIndividualDefault));
        }
        return result.OrderBy(r => r.StudentName).ToList();
    }
}

// ======================= Önizleme (dry-run + rollback = apply ile birebir) =======================
public sealed record PreviewGroupScheduleChangeQuery(
    Guid InstructorProfileId, Guid ClassTypeId, DateOnly EffectiveDate,
    IReadOnlyList<Guid> ContractIds, IReadOnlyList<ContractScheduleSlotInput> NewSlots) : IQuery<GroupChangePreviewDto>;

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
                request.ContractIds, request.NewSlots, ct);
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
    IReadOnlyList<Guid> ContractIds, IReadOnlyList<ContractScheduleSlotInput> NewSlots)
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
            request.ContractIds, request.NewSlots, ct);

        // HARD çakışma varsa atomik abort: exception → TransactionBehavior tüm işlemi rollback eder (yarım taşıma yok).
        if (!result.CanApply)
            throw new ConflictException("Çakışma var; grup değişikliği uygulanamadı. Önizlemeyi kontrol edin (hoca/öğrenci çakışması).");

        return result;
    }
}
