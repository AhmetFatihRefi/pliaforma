using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Equipments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Application.Features.Studios;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Contracts;

// =====================================================================================================
// Sprint 34 — Sözleşme satırı TERCİH güncelleme + kalan ders programını YENİDEN PLANLAMA (replan).
// İki ayrı operasyon (karar #4):
//   A) UpdateContractLinePreferences — hoca/kapasite/slot kaydeder; MEVCUT booking'lere DOKUNMAZ (karar #7).
//   B) Replan (preview + apply) — gelecekteki (studioNow+) Booked/Regular/Scheduled booking'leri TELAFİSİZ ayırır,
//      boşalan seansları iptal eder (shared-safe: §6), satırın GÜNCEL tercihleriyle RunAsync ile yeniden üretir.
// Migration YOK: tüm alanlar/ilişkiler mevcut. Completed/Delivered/geçmiş ASLA değişmez. Makeup kapsam dışı.
// Yetki: Owner/StudioAdmin (controller). Yalnız Active line (karar #8).
// =====================================================================================================

internal static class ContractLineReplanShared
{
    /// <summary>Aktif satırı yükler; non-active ise kullanıcı dostu 409 (karar #8).</summary>
    public static async Task<StudentContractLine> LoadActiveLineAsync(IAppDbContext db, Guid studioId, Guid lineId, string action, CancellationToken ct)
    {
        var line = await db.StudentContractLines.Include(l => l.StudentContract)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme satırı", lineId);
        if (line.Status != StudentContractLineStatus.Active)
            throw new ConflictException($"Yalnızca aktif sözleşme satırı {action}. Bu satır {line.Status} durumunda.");
        return line;
    }

    /// <summary>Satırın AKTİF + SAATLİ tercih slotlarını üretim slotuna çevirir (saatsiz = yalnız gün tercihi, üretilemez).</summary>
    public static Task<List<ContractScheduleSlotInput>> DeriveGenerationSlotsAsync(IAppDbContext db, Guid lineId, CancellationToken ct) =>
        db.StudentContractLinePreferredSlots
            .Where(s => s.StudentContractLineId == lineId && s.IsActive && s.PreferredStartTime != null)
            .OrderBy(s => s.SequenceNo)
            .Select(s => new ContractScheduleSlotInput(s.DayOfWeek, s.PreferredStartTime!.Value))
            .ToListAsync(ct);

    /// <summary>Satırın gelecekteki (studioNow+) Booked/Regular/Scheduled booking'lerinin seans id'leri (replan'da iptal edilecekler).</summary>
    public static Task<List<Guid>> FutureRegularSessionIdsAsync(IAppDbContext db, Guid studioId, Guid lineId, DateTime nowLocal, CancellationToken ct) =>
        db.ClassBookings
            .Where(b => b.StudioId == studioId && b.StudentContractLineId == lineId && b.Status == ClassBookingStatus.Booked
                        && b.ClassSession!.SessionKind == ClassSessionKind.Regular
                        && b.ClassSession.Status == ClassSessionStatus.Scheduled
                        && b.ClassSession.StartAt >= nowLocal)
            .Select(b => b.ClassSessionId)
            .ToListAsync(ct);
}

// ===================================================================================================
// A) TERCİH GÜNCELLE — PUT /contract-lines/{lineId}/preferences. Booking'e dokunmaz.
// ===================================================================================================
public sealed record UpdateContractLinePreferencesCommand(
    Guid LineId,
    // null = herhangi bir hoca; dolu = hard hoca kuralı (aktif stüdyoya ait olmalı).
    Guid? InstructorProfileId,
    // null = farketmez; 1 = bireysel; 2..50 = en fazla N.
    int? PreferredMaxCapacity,
    // FULL REPLACE — verilen liste yeni durum olur (≥1; aynı gün+saat tekrar edemez).
    IReadOnlyList<PreferredSlotInput> PreferredSlots) : ICommand<StudentContractLineDto>, IStudioWriteRequest;

public sealed class UpdateContractLinePreferencesValidator : AbstractValidator<UpdateContractLinePreferencesCommand>
{
    public UpdateContractLinePreferencesValidator()
    {
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.PreferredMaxCapacity).InclusiveBetween(1, 50).When(x => x.PreferredMaxCapacity.HasValue)
            .WithMessage("Kapasite tercihi 1 ile 50 arasında olmalıdır.");
        RuleFor(x => x.PreferredSlots).NotEmpty().WithMessage("En az bir tercih gün/saat girilmeli.");
        RuleFor(x => x.PreferredSlots)
            .Must(s => !PreferredSlotRules.HasInternalDuplicates(s!)).When(x => x.PreferredSlots is { Count: > 0 })
            .WithMessage("Aynı gün ve saat tercihi tekrar edemez.");
    }
}

public sealed class UpdateContractLinePreferencesHandler : ICommandHandler<UpdateContractLinePreferencesCommand, StudentContractLineDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public UpdateContractLinePreferencesHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudentContractLineDto> Handle(UpdateContractLinePreferencesCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var line = await ContractLineReplanShared.LoadActiveLineAsync(_db, studioId, request.LineId, "güncellenebilir", ct);

        // Hoca opsiyonel — verildiyse aktif stüdyoya ait olmalı; null = herhangi bir hoca.
        Guid? instructorId = null;
        if (request.InstructorProfileId is { } reqInstr)
        {
            var exists = await _db.InstructorProfiles.AnyAsync(p => p.Id == reqInstr && p.StudioId == studioId, ct);
            if (!exists) throw new NotFoundException("Hoca", reqInstr);
            instructorId = reqInstr;
        }

        // Tercihleri kaydet — IsIndividual PreferredMaxCapacity'den türetilir (Sprint 33). Booking'lere DOKUNMA (karar #7).
        line.InstructorProfileId = instructorId;
        line.PreferredMaxCapacity = request.PreferredMaxCapacity;
        line.IsIndividual = request.PreferredMaxCapacity == 1;

        // Slot FULL REPLACE: mevcut aktif slotları soft-delete + yenilerini ekle (tek transaction — TransactionBehavior).
        var existing = await _db.StudentContractLinePreferredSlots
            .Where(s => s.StudentContractLineId == line.Id).ToListAsync(ct);
        _db.StudentContractLinePreferredSlots.RemoveRange(existing); // AuditInterceptor → soft-delete
        foreach (var slot in PreferredSlotRules.Build(studioId, line.Id, userId, request.PreferredSlots))
            _db.StudentContractLinePreferredSlots.Add(slot);

        await _db.SaveChangesAsync(ct);

        var dtos = await ContractLineQuery.BuildAsync(_db, _db.StudentContractLines.AsNoTracking().Where(l => l.Id == line.Id), ct);
        return dtos[0];
    }
}

// ===================================================================================================
// B1) REPLAN PREVIEW — POST /contract-lines/{lineId}/replan/preview. Yıkıcı değil (DB değişmez).
// ===================================================================================================
public sealed record ReplanLinePreviewDto(
    int DeliveredCount,                 // tamamlanan (Completed) — DOKUNULMAZ
    int RemainingLessonRight,           // LessonCount − Delivered
    int FutureBookingsToCancel,         // iptal edilecek gelecekteki planlı booking
    int PlannableCount,                 // yeniden üretilebilecek ders (tahmini; apply yetkilidir)
    int NotPlannableCount,              // availableAfter − plannable (uygunluk yok)
    Guid? EffectiveInstructorProfileId,
    string? EffectiveInstructorName,
    int? EffectiveCapacity,             // 9999 = sınırsız grup; null = çözülemedi (uyarı var)
    IReadOnlyList<ContractScheduleSlotPreviewDto> Slots,
    IReadOnlyList<string> Warnings);

public sealed record ReplanLinePreviewQuery(Guid LineId, Guid? InstructorProfileId, int? Capacity) : IQuery<ReplanLinePreviewDto>;

public sealed class ReplanLinePreviewHandler : IQueryHandler<ReplanLinePreviewQuery, ReplanLinePreviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public ReplanLinePreviewHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<ReplanLinePreviewDto> Handle(ReplanLinePreviewQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var line = await ContractLineReplanShared.LoadActiveLineAsync(_db, studioId, request.LineId, "yeniden planlanabilir", ct);
        var studentId = line.StudentContract!.StudentProfileId;
        var warnings = new List<string>();

        var delivered = await ContractLineRules.DeliveredRegularAsync(_db, line, ct);
        var available = await ContractLineRules.AvailableAsync(_db, line, ct);
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);
        var toCancel = await ContractLineReplanShared.FutureRegularSessionIdsAsync(_db, studioId, line.Id, nowLocal, ct);
        var toCancelSet = toCancel.ToHashSet();
        var futureToCancel = toCancel.Count;
        var availableAfter = available + futureToCancel; // iptal sonrası planlanabilir hak
        var remainingRight = Math.Max(0, line.LessonCount - delivered);

        // Effective hoca (line ?? üretim-anı) + ad.
        var effInstr = line.InstructorProfileId ?? request.InstructorProfileId;
        string? effInstrName = null;
        if (effInstr is null) warnings.Add("Hoca belirtilmemiş; yeniden planlama için bir hoca seçin.");
        else
        {
            effInstrName = await _db.InstructorProfiles.Where(p => p.Id == effInstr && p.StudioId == studioId)
                .Select(p => (string?)p.DisplayName).FirstOrDefaultAsync(ct);
            if (effInstrName is null) warnings.Add("Seçilen hoca bu stüdyoda bulunamadı.");
        }

        // Effective kapasite (Sprint 33 kuralı).
        var classType = await _db.ClassTypes.FirstAsync(c => c.Id == line.ClassTypeId, ct);
        int? effCap = null;
        if (line.PreferredMaxCapacity is { } pref) effCap = pref;
        else if (classType.EquipmentId is not null)
        {
            if (request.Capacity is { } gc) effCap = gc;
            else warnings.Add("Ekipman bağlı ders; yeniden planlama için seans kapasitesi seçin.");
        }
        else effCap = ContractScheduleEngine.GroupUnlimitedCapacity;
        if (effCap is { } cap && classType.EquipmentId is not null && cap >= ContractScheduleEngine.GroupUnlimitedCapacity)
            warnings.Add("Ekipman bağlı derste sınırsız kapasiteyle program üretilemez.");

        var slots = await ContractLineReplanShared.DeriveGenerationSlotsAsync(_db, line.Id, ct);
        if (slots.Count == 0) warnings.Add("Saat belirtilmiş gün/saat tercihi yok; bu tercihlerle ders üretilemez.");

        var slotDtos = new List<ContractScheduleSlotPreviewDto>();
        var plannable = 0;
        // Üretim ancak hoca + ad çözülmüş, kapasite çözülmüş ve saatli slot varsa simüle edilir (aksi halde warning + 0).
        var canSimulate = effInstr is not null && effInstrName is not null && effCap is not null && slots.Count > 0;
        if (canSimulate)
        {
            var instr = effInstr!.Value;
            var duration = classType.DefaultDurationMinutes;
            var closures = await _db.StudioClosures.Where(cl => cl.StudioId == studioId).ToListAsync(ct);
            var workingHours = await StudioWorkingHoursService.LoadAsync(_db, studioId, ct);
            var candidates = ContractLineScheduleEngine.BuildCandidates(line, duration, slots);

            // İptal edilecek seansları çakışma kontrolünden HARİÇ tut (apply'da zaten ayrılacaklar) → doğru tahmin.
            async Task<bool> InstrBusy(DateTime s, DateTime e) => await _db.ClassSessions.AnyAsync(x =>
                x.StudioId == studioId && x.InstructorProfileId == instr && x.Status == ClassSessionStatus.Scheduled
                && !toCancelSet.Contains(x.Id) && x.StartAt < e && s < x.EndAt, ct);
            async Task<bool> StudBusy(DateTime s, DateTime e) => await _db.ClassBookings.AnyAsync(b =>
                b.StudioId == studioId && b.StudentProfileId == studentId && b.Status == ClassBookingStatus.Booked
                && b.ClassSession!.Status == ClassSessionStatus.Scheduled && !toCancelSet.Contains(b.ClassSessionId)
                && b.ClassSession.StartAt < e && s < b.ClassSession.EndAt, ct);

            foreach (var c in candidates)
            {
                if (plannable >= availableAfter) break;
                string? skip = null;
                if (c.StartAt <= nowLocal) skip = "Geçmiş zaman";
                else if (ContractScheduleEngine.InAnyClosure(c.StartAt, closures)) skip = "Stüdyo kapalı (tatil/kapanış)";
                else if (!workingHours.Fits(c.StartAt, c.EndAt)) skip = "Çalışma saati dışında";
                else if (await InstrBusy(c.StartAt, c.EndAt)) skip = "Hoca çakışması";
                else if (await StudBusy(c.StartAt, c.EndAt)) skip = "Öğrenci çakışması";

                slotDtos.Add(new ContractScheduleSlotPreviewDto(c.StartAt, c.EndAt, skip is null, skip));
                if (skip is null) plannable++;
            }
            if (slotDtos.Any(s => s.SkipReason == "Çalışma saati dışında"))
                warnings.Add("Bazı tercih slotları stüdyo çalışma saatleri dışında; bu dersler planlanamaz.");
        }

        var notPlannable = Math.Max(0, availableAfter - plannable);
        return new ReplanLinePreviewDto(delivered, remainingRight, futureToCancel, plannable, notPlannable,
            effInstr, effInstrName, effCap, slotDtos, warnings);
    }
}

// ===================================================================================================
// B2) REPLAN APPLY — POST /contract-lines/{lineId}/replan. detach + regenerate (Strategy A).
// ===================================================================================================
public sealed record ReplanLineResultDto(
    int CancelledFutureBookings,
    int EmptiedSessionsCancelled,
    int CreatedCount,
    int NewSessionCount,
    int ReusedSessionCount,
    int SkippedCount,
    int NotPlannedCount,                // iptal sonrası planlanamayan kalan ders
    IReadOnlyList<DateTime> CreatedStartAts,
    int SkippedOutsideWorkingHoursCount = 0);  // Sprint 37: çalışma saati dışı kalan aday sayısı

public sealed record ReplanLineScheduleCommand(
    Guid LineId,
    // Sprint 33: line hocası null ise üretim-anı hoca; ekipman-bağlı + tercih null ise üretim-anı kapasite.
    Guid? InstructorProfileId, int? Capacity) : ICommand<ReplanLineResultDto>, IStudioWriteRequest;

public sealed class ReplanLineScheduleValidator : AbstractValidator<ReplanLineScheduleCommand>
{
    public ReplanLineScheduleValidator()
    {
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.Capacity).InclusiveBetween(1, 50).When(x => x.Capacity.HasValue)
            .WithMessage("Seans kapasitesi 1 ile 50 arasında olmalıdır.");
    }
}

public sealed class ReplanLineScheduleHandler : ICommandHandler<ReplanLineScheduleCommand, ReplanLineResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly EquipmentCapacityGuard _equipmentGuard;
    public ReplanLineScheduleHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, EquipmentCapacityGuard equipmentGuard)
    { _db = db; _currentUser = currentUser; _clock = clock; _equipmentGuard = equipmentGuard; }

    public async Task<ReplanLineResultDto> Handle(ReplanLineScheduleCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var line = await ContractLineReplanShared.LoadActiveLineAsync(_db, studioId, request.LineId, "yeniden planlanabilir", ct);

        // Üretim-anı hoca seçildiyse aktif stüdyoya ait olmalı (line hocası null senaryosu).
        if (request.InstructorProfileId is { } gi && !await _db.InstructorProfiles.AnyAsync(p => p.Id == gi && p.StudioId == studioId, ct))
            throw new NotFoundException("Hoca", gi);

        var slots = await ContractLineReplanShared.DeriveGenerationSlotsAsync(_db, line.Id, ct);
        if (slots.Count == 0)
            throw new ConflictException("Yeniden planlama için saat belirtilmiş en az bir gün/saat tercihi gerekli.");

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);

        // ---- PHASE 1: gelecekteki Booked/Regular/Scheduled booking'leri TELAFİSİZ ayır (Completed/geçmiş DOKUNULMAZ). ----
        var toRemove = await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.StudentContractLineId == line.Id && b.Status == ClassBookingStatus.Booked
                        && b.ClassSession!.SessionKind == ClassSessionKind.Regular
                        && b.ClassSession.Status == ClassSessionStatus.Scheduled
                        && b.ClassSession.StartAt >= nowLocal)
            .ToListAsync(ct);
        var affected = new HashSet<Guid>();
        foreach (var b in toRemove)
        {
            b.Status = ClassBookingStatus.CancelledByStudio;
            b.CancelledByUserId = userId;
            b.CancelledAt = _clock.UtcNow;
            b.CancellationReason = "Sözleşme tercih değişikliği — yeniden planlandı";
            affected.Add(b.ClassSessionId);
        }
        await _db.SaveChangesAsync(ct);

        // ---- PHASE 2: etkilenen seansların BookedCount'unu güncelle; YALNIZ tamamen boşalanı iptal et (shared-safe §6). ----
        var emptied = 0;
        foreach (var sid in affected)
        {
            var remaining = await _db.ClassBookings.CountAsync(b => b.ClassSessionId == sid && b.Status == ClassBookingStatus.Booked, ct);
            var sess = await _db.ClassSessions.FirstOrDefaultAsync(s => s.Id == sid, ct);
            if (sess is null) continue;
            sess.BookedCount = remaining;
            if (remaining == 0 && sess.Status == ClassSessionStatus.Scheduled)
            {
                sess.Status = ClassSessionStatus.Cancelled;
                sess.Notes = "Tercih değişikliği — boş kaldı";
                emptied++;
            }
        }
        await _db.SaveChangesAsync(ct);

        // ---- PHASE 3: satırın GÜNCEL slot/hoca/kapasitesiyle yeniden üret (RunAsync = tüm S32/S33 guard'ları). ----
        var availableBefore = await ContractLineRules.AvailableAsync(_db, line, ct); // detach sonrası artmış hak
        var duration = await _db.ClassTypes.Where(c => c.Id == line.ClassTypeId).Select(c => c.DefaultDurationMinutes).FirstAsync(ct);
        var gen = await ContractLineScheduleEngine.RunAsync(
            _db, _clock, studioId, userId, line, duration, slots, reuseExistingGroup: true, generate: true, ct,
            _equipmentGuard, request.InstructorProfileId, request.Capacity);

        var notPlanned = Math.Max(0, availableBefore - gen.CreatedCount);
        return new ReplanLineResultDto(
            toRemove.Count, emptied, gen.CreatedCount, gen.NewSessionCount, gen.ReusedSessionCount, gen.SkippedCount,
            notPlanned, gen.CreatedStartAts, gen.SkippedOutsideWorkingHoursCount);
    }
}
