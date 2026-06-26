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
// Sprint 28C-1 — Regular scheduling/booking LINE-bazlı. Program + booking artık StudentContractLine üzerinden
// çalışır: hak/tarih-aralığı/bireysellik/hoca line'dan gelir; ClassBooking.StudentContractLineId yazılır.
//
// Rezerve (reserved Regular) LEGACY-FALLBACK'li (28B paid deseninin aynısı — karar #7):
//   reserved(L) = Σ Booking(Booked,Regular, lineId=L)
//               + ( header(L)'de TAM 1 line varsa: Σ Booking(Booked,Regular, lineId=NULL, contractId=L.header) )
// Çok-line header'da line'sız booking HİÇBİR line'a sayılmaz → çift sayım yok. available = LessonCount − reserved.
// =====================================================================================================

internal static class ContractLineRules
{
    /// <summary>Line'ın rezerve (Booked + Regular) ders sayısı — legacy null-line fallback yalnız tek-line header'da.</summary>
    public static async Task<int> ReservedRegularAsync(IAppDbContext db, StudentContractLine line, CancellationToken ct)
    {
        var direct = await db.ClassBookings.CountAsync(b => b.StudentContractLineId == line.Id
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular, ct);

        var headerLineCount = await db.StudentContractLines.CountAsync(x => x.StudentContractId == line.StudentContractId, ct);
        if (headerLineCount != 1)
            return direct; // çok-line header → line'sız booking'ler hiçbir line'a sayılmaz

        var legacy = await db.ClassBookings.CountAsync(b => b.StudentContractLineId == null
            && b.StudentContractId == line.StudentContractId
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular, ct);
        return direct + legacy;
    }

    public static async Task<int> AvailableAsync(IAppDbContext db, StudentContractLine line, CancellationToken ct)
        => Math.Max(0, line.LessonCount - await ReservedRegularAsync(db, line, ct));

    /// <summary>Sprint 29C: PLANLANMIŞ (Booked + Regular + seans Scheduled) ders sayısı — legacy null-line fallback yalnız tek-line header'da.</summary>
    public static async Task<int> PlannedRegularAsync(IAppDbContext db, StudentContractLine line, CancellationToken ct)
    {
        var direct = await db.ClassBookings.CountAsync(b => b.StudentContractLineId == line.Id
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular
            && b.ClassSession!.Status == ClassSessionStatus.Scheduled, ct);
        var headerLineCount = await db.StudentContractLines.CountAsync(x => x.StudentContractId == line.StudentContractId, ct);
        if (headerLineCount != 1) return direct;
        var legacy = await db.ClassBookings.CountAsync(b => b.StudentContractLineId == null
            && b.StudentContractId == line.StudentContractId
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular
            && b.ClassSession!.Status == ClassSessionStatus.Scheduled, ct);
        return direct + legacy;
    }

    /// <summary>Sprint 29C: İŞLENMİŞ (Booked + Regular + seans Completed) ders sayısı — D1: Absent de tüketir (booking Booked kalır).</summary>
    public static async Task<int> DeliveredRegularAsync(IAppDbContext db, StudentContractLine line, CancellationToken ct)
    {
        var direct = await db.ClassBookings.CountAsync(b => b.StudentContractLineId == line.Id
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular
            && b.ClassSession!.Status == ClassSessionStatus.Completed, ct);
        var headerLineCount = await db.StudentContractLines.CountAsync(x => x.StudentContractId == line.StudentContractId, ct);
        if (headerLineCount != 1) return direct;
        var legacy = await db.ClassBookings.CountAsync(b => b.StudentContractLineId == null
            && b.StudentContractId == line.StudentContractId
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular
            && b.ClassSession!.Status == ClassSessionStatus.Completed, ct);
        return direct + legacy;
    }

    /// <summary>
    /// Regular seansın yeni saati, o seansa bağlı Booked booking'lerin line tarih aralığında olmalı
    /// (karar #5 — hard block). Makeup seans MUAF (28C-2'de line-aware). Line'ı NULL booking'ler atlanır (legacy/geçiş).
    /// </summary>
    public static async Task EnsureRegularInLineRangeAsync(
        IAppDbContext db, Guid studioId, ClassSession session, DateTime newStartAt, CancellationToken ct)
    {
        if (session.SessionKind != ClassSessionKind.Regular) return;
        var newDate = DateOnly.FromDateTime(newStartAt);

        var ranges = await db.ClassBookings
            .Where(b => b.StudioId == studioId && b.ClassSessionId == session.Id
                        && b.Status == ClassBookingStatus.Booked && b.StudentContractLineId != null)
            .Select(b => new { b.StudentContractLine!.StartDate, b.StudentContractLine!.EndDate })
            .ToListAsync(ct);

        foreach (var r in ranges)
        {
            if (newDate < r.StartDate || newDate > r.EndDate)
                throw new ConflictException("Ders, sözleşme satırı (line) tarih aralığını aşıyor; bu tarihe taşınamaz.");
        }
    }
}

// =====================================================================================================
// Resolver — bir Regular rezervasyon için UYGUN aktif LINE'ı otomatik bulur (karar #2: instructor ZORUNLU).
//   studentProfileId + classTypeId + sessionDate ∈ [StartDate,EndDate] + Status=Active + available>0
//   + line.InstructorProfileId == session.InstructorProfileId. Deterministik: en erken biten → SequenceNo → CreatedAt.
// =====================================================================================================
public sealed class ContractLineResolver
{
    private readonly IAppDbContext _db;
    public ContractLineResolver(IAppDbContext db) => _db = db;

    public async Task<StudentContractLine?> ResolveAsync(
        Guid studioId, Guid studentProfileId, Guid classTypeId, DateOnly sessionDate, Guid instructorProfileId,
        CancellationToken ct, Guid? headerId = null)
    {
        var candidates = await _db.StudentContractLines
            .Include(l => l.StudentContract)
            .Where(l => l.StudioId == studioId
                        && l.StudentContract!.StudentProfileId == studentProfileId
                        && (headerId == null || l.StudentContractId == headerId)
                        && l.ClassTypeId == classTypeId
                        // Sprint 33: line hocası null → herhangi bir hoca; dolu → eşleşme zorunlu.
                        && (l.InstructorProfileId == null || l.InstructorProfileId == instructorProfileId)
                        && l.Status == StudentContractLineStatus.Active
                        && l.StartDate <= sessionDate && sessionDate <= l.EndDate)
            .OrderBy(l => l.EndDate).ThenBy(l => l.SequenceNo).ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);

        foreach (var l in candidates)
            if (await ContractLineRules.AvailableAsync(_db, l, ct) > 0)
                return l;
        return null;
    }
}

// =====================================================================================================
// Eligibility — verilen line bu Regular rezervasyondan çekilebilir mi (staff line seçtiyse). Instructor eşleşmesi ZORUNLU.
// =====================================================================================================
public sealed record ContractLineEligibilityResult(bool IsEligible, string? Message, StudentContractLine? Line);

public sealed class ContractLineEligibilityChecker
{
    private readonly IAppDbContext _db;
    public ContractLineEligibilityChecker(IAppDbContext db) => _db = db;

    public async Task<ContractLineEligibilityResult> EvaluateAsync(
        Guid studioId, Guid lineId, Guid studentProfileId, Guid classTypeId, DateOnly sessionDate, Guid instructorProfileId, CancellationToken ct)
    {
        var line = await _db.StudentContractLines
            .Include(l => l.StudentContract)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.StudioId == studioId, ct);

        if (line is null)
            return new(false, "Sözleşme satırı bulunamadı.", null);
        if (line.Status != StudentContractLineStatus.Active)
            return new(false, "Sözleşme satırı aktif değil.", null);
        if (line.StudentContract!.StudentProfileId != studentProfileId)
            return new(false, "Sözleşme satırı bu öğrenciye ait değil.", null);
        if (line.ClassTypeId != classTypeId)
            return new(false, "Sözleşme satırı bu ders grubu için geçerli değil.", null);
        // Sprint 33: line hocası null → herhangi bir hoca; dolu → eşleşme zorunlu.
        if (line.InstructorProfileId is { } pin && pin != instructorProfileId)
            return new(false, "Sözleşme satırı bu hoca için geçerli değil (hoca eşleşmiyor).", null);
        if (sessionDate < line.StartDate || sessionDate > line.EndDate)
            return new(false, "Ders tarihi sözleşme satırı aralığı dışında.", null);
        if (await ContractLineRules.AvailableAsync(_db, line, ct) <= 0)
            return new(false, "Sözleşme satırı ders hakkı kalmadı.", null);

        return new(true, null, line);
    }
}

// =====================================================================================================
// Line schedule engine — line üzerinden aday üretir; line.IsIndividual'a göre bireysel/grup; booking lineId yazar.
// =====================================================================================================
internal static class ContractLineScheduleEngine
{
    public static List<ContractScheduleEngine.Candidate> BuildCandidates(
        StudentContractLine line, int durationMin, IEnumerable<ContractScheduleSlotInput> slots)
    {
        var list = new List<ContractScheduleEngine.Candidate>();
        var bySlot = slots.ToList();
        for (var date = line.StartDate; date <= line.EndDate; date = date.AddDays(1))
            foreach (var slot in bySlot.Where(s => s.DayOfWeek == date.DayOfWeek))
            {
                var start = date.ToDateTime(slot.StartTime, DateTimeKind.Unspecified);
                list.Add(new ContractScheduleEngine.Candidate(start, start.AddMinutes(durationMin)));
            }
        return list.OrderBy(c => c.StartAt).ToList();
    }

    public static ClassBooking NewBooking(StudentContractLine line, Guid sessionId, Guid userId) =>
        new()
        {
            StudioId = line.StudioId,
            ClassSessionId = sessionId,
            StudentProfileId = line.StudentContract!.StudentProfileId,
            StudentContractId = line.StudentContractId,
            StudentContractLineId = line.Id,
            Status = ClassBookingStatus.Booked,
            BookedByUserId = userId,
        };

    /// <summary>Bir line için program üretir (ortak çekirdek; preview ve generate paylaşır). Generate=false → yazma yok.</summary>
    public static async Task<GenerateContractScheduleResultDto> RunAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, Guid userId,
        StudentContractLine line, int durationMin, IReadOnlyList<ContractScheduleSlotInput> slots,
        bool reuseExistingGroup, bool generate, CancellationToken ct,
        EquipmentCapacityGuard? equipmentGuard = null,
        Guid? generationInstructorId = null, int? generationCapacity = null)
    {
        var available = await ContractLineRules.AvailableAsync(db, line, ct);
        if (available == 0)
            return new GenerateContractScheduleResultDto(0, 0, 0, 0, Array.Empty<DateTime>(), 0);

        // Sprint 33: effective hoca = line tercihi ?? üretim-anı seçimi. İkisi de yoksa üretilemez (bir seansın hocası olmak zorunda).
        var effectiveInstructor = line.InstructorProfileId ?? generationInstructorId
            ?? throw new ConflictException("Bu sözleşmenin hocası belirtilmemiş; program oluşturmak için bir hoca seçin.");

        // Sprint 33: effective kapasite. PreferredMaxCapacity ?? (ekipman-bağlı → üretim kapasitesi ZORUNLU : grup-sınırsız 9999).
        var classType = await db.ClassTypes.FirstAsync(c => c.Id == line.ClassTypeId, ct);
        int effectiveCapacity;
        if (line.PreferredMaxCapacity is { } pref) effectiveCapacity = pref;
        else if (classType.EquipmentId is not null)
            effectiveCapacity = generationCapacity
                ?? throw new ConflictException("Ekipman bağlı ders için sınırsız program üretilemez; program oluştururken sonlu bir seans kapasitesi seçin.");
        else effectiveCapacity = ContractScheduleEngine.GroupUnlimitedCapacity;
        EquipmentCapacityRules.EnsureFiniteForEquipment(classType, effectiveCapacity); // 32B invariant güvencesi
        var canReuse = effectiveCapacity > 1; // bireysel (1) → reuse yok

        var nowLocal = await SessionRules.StudioNowAsync(db, clock, studioId, ct);
        var closures = await db.StudioClosures.Where(cl => cl.StudioId == studioId).ToListAsync(ct);
        // Sprint 37: çalışma saatleri (kayıt yoksa fallback 07:00–23:00). Pencere dışı adaylar ayrı sayaçta atlanır.
        var workingHours = await StudioWorkingHoursService.LoadAsync(db, studioId, ct);
        var candidates = BuildCandidates(line, durationMin, slots);
        var studentProfileId = line.StudentContract!.StudentProfileId;

        var created = new List<DateTime>();
        int newSessions = 0, reusedSessions = 0, skipped = 0, skippedOutsideHours = 0;
        var batchInstructor = new List<(DateTime s, DateTime e)>();
        var batchStudent = new List<(DateTime s, DateTime e)>();

        foreach (var c in candidates)
        {
            if (created.Count >= available) break;
            if (c.StartAt <= nowLocal) { skipped++; continue; }
            if (ContractScheduleEngine.InAnyClosure(c.StartAt, closures)) { skipped++; continue; }
            // Sprint 37: çalışma saati dışı (kapalı gün / pencere dışı / kapanış aşımı) → ayrı sayaç.
            if (!workingHours.Fits(c.StartAt, c.EndAt)) { skippedOutsideHours++; continue; }

            ClassSession? target = null;
            if (canReuse && reuseExistingGroup)
                target = await ContractScheduleEngine.FindReusableGroupAsync(db, studioId, effectiveInstructor, line.ClassTypeId, c.StartAt, ct, effectiveCapacity);

            var instructorBusy = batchInstructor.Any(x => x.s < c.EndAt && c.StartAt < x.e)
                || await SessionRules.HasConflictAsync(db, studioId, effectiveInstructor, c.StartAt, c.EndAt, null, ct);
            if (target is null && instructorBusy) { skipped++; continue; }

            var studentBusy = batchStudent.Any(x => x.s < c.EndAt && c.StartAt < x.e)
                || await ContractScheduleEngine.StudentBusyAsync(db, studioId, studentProfileId, c.StartAt, c.EndAt, ct);
            if (studentBusy) { skipped++; continue; }

            if (generate)
            {
                if (target is null)
                {
                    target = ContractScheduleEngine.NewSession(studioId, effectiveInstructor, line.ClassTypeId, c.StartAt, c.EndAt, effectiveCapacity, userId);
                    // Sprint 32B/33: ekipman-bağlı her yeni (sonlu kapasiteli) seans → guard (availability + havuz kilidi). Dolu → 409.
                    if (classType.EquipmentId is not null && equipmentGuard is not null)
                        await equipmentGuard.EnsureCanReserveAsync(studioId, classType, c.StartAt, c.EndAt, target.Capacity, excludeSessionId: null, pendingOverlapCapacity: 0, ct);
                    db.ClassSessions.Add(target);
                    newSessions++;
                }
                else reusedSessions++;
                target.BookedCount += 1;
                db.ClassBookings.Add(NewBooking(line, target.Id, userId));
            }
            else
            {
                if (target is null) newSessions++; else reusedSessions++;
            }

            batchInstructor.Add((c.StartAt, c.EndAt));
            batchStudent.Add((c.StartAt, c.EndAt));
            created.Add(c.StartAt);
        }

        if (generate) await db.SaveChangesAsync(ct);
        return new GenerateContractScheduleResultDto(created.Count, newSessions, reusedSessions, skipped, created, skippedOutsideHours);
    }

    public static async Task<StudentContractLine> LoadActiveLineAsync(IAppDbContext db, Guid studioId, Guid lineId, CancellationToken ct)
    {
        var line = await db.StudentContractLines.Include(l => l.StudentContract)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme satırı", lineId);
        if (line.Status != StudentContractLineStatus.Active)
            throw new BusinessRuleException("Yalnızca aktif sözleşme satırından program üretilebilir.");
        return line;
    }
}

// ======================= LINE ÖNİZLEME =======================
public sealed record PreviewLineScheduleQuery(Guid LineId, IReadOnlyList<ContractScheduleSlotInput> Slots)
    : IQuery<ContractSchedulePreviewDto>;

public sealed class PreviewLineScheduleHandler : IQueryHandler<PreviewLineScheduleQuery, ContractSchedulePreviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public PreviewLineScheduleHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<ContractSchedulePreviewDto> Handle(PreviewLineScheduleQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var line = await ContractLineScheduleEngine.LoadActiveLineAsync(_db, studioId, request.LineId, ct);
        var duration = await _db.ClassTypes.Where(c => c.Id == line.ClassTypeId).Select(c => c.DefaultDurationMinutes).FirstAsync(ct);

        var available = await ContractLineRules.AvailableAsync(_db, line, ct);
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);
        var closures = await _db.StudioClosures.Where(cl => cl.StudioId == studioId).ToListAsync(ct);
        var workingHours = await StudioWorkingHoursService.LoadAsync(_db, studioId, ct);
        var candidates = ContractLineScheduleEngine.BuildCandidates(line, duration, request.Slots);

        var slots = new List<ContractScheduleSlotPreviewDto>();
        var planned = 0;
        foreach (var c in candidates)
        {
            if (planned >= available) break;
            string? skip = null;
            if (c.StartAt <= nowLocal) skip = "Geçmiş zaman";
            else if (ContractScheduleEngine.InAnyClosure(c.StartAt, closures)) skip = "Stüdyo kapalı (tatil/kapanış)";
            else if (!workingHours.Fits(c.StartAt, c.EndAt)) skip = "Çalışma saati dışında";
            // Sprint 33: line hocası null ise hoca çakışması önizlemede değerlendirilemez (üretimde hoca seçilir).
            else if (line.InstructorProfileId is { } instr
                     && await SessionRules.HasConflictAsync(_db, studioId, instr, c.StartAt, c.EndAt, null, ct)
                     && (line.IsIndividual || await ContractScheduleEngine.FindReusableGroupAsync(_db, studioId, instr, line.ClassTypeId, c.StartAt, ct, line.PreferredMaxCapacity ?? int.MaxValue) is null))
                skip = "Hoca çakışması";
            else if (await ContractScheduleEngine.StudentBusyAsync(_db, studioId, line.StudentContract!.StudentProfileId, c.StartAt, c.EndAt, ct))
                skip = "Öğrenci çakışması";

            var plannable = skip is null;
            slots.Add(new ContractScheduleSlotPreviewDto(c.StartAt, c.EndAt, plannable, skip));
            if (plannable) planned++;
        }
        return new ContractSchedulePreviewDto(available, planned, slots);
    }
}

// ======================= LINE ÜRETİM =======================
public sealed record GenerateLineScheduleCommand(
    Guid LineId, IReadOnlyList<ContractScheduleSlotInput> Slots, bool ReuseExistingGroup,
    // Sprint 33: line hocası null ise üretim-anı hoca; line PreferredMaxCapacity null + ekipman-bağlı ise üretim-anı kapasite.
    Guid? InstructorProfileId = null, int? Capacity = null)
    : ICommand<GenerateContractScheduleResultDto>, IStudioWriteRequest;

public sealed class GenerateLineScheduleValidator : AbstractValidator<GenerateLineScheduleCommand>
{
    public GenerateLineScheduleValidator()
    {
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.Slots).NotEmpty().WithMessage("En az bir gün/saat seçilmeli.");
        RuleFor(x => x.Capacity).InclusiveBetween(1, 50).When(x => x.Capacity.HasValue)
            .WithMessage("Seans kapasitesi 1 ile 50 arasında olmalıdır.");
    }
}

public sealed class GenerateLineScheduleHandler : ICommandHandler<GenerateLineScheduleCommand, GenerateContractScheduleResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly EquipmentCapacityGuard _equipmentGuard;
    public GenerateLineScheduleHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, EquipmentCapacityGuard equipmentGuard)
    { _db = db; _currentUser = currentUser; _clock = clock; _equipmentGuard = equipmentGuard; }

    public async Task<GenerateContractScheduleResultDto> Handle(GenerateLineScheduleCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var line = await ContractLineScheduleEngine.LoadActiveLineAsync(_db, studioId, request.LineId, ct);
        var duration = await _db.ClassTypes.Where(c => c.Id == line.ClassTypeId).Select(c => c.DefaultDurationMinutes).FirstAsync(ct);
        // Sprint 33: üretim-anı hoca seçildiyse aktif stüdyoya ait olmalı (line hocası null senaryosu).
        if (request.InstructorProfileId is { } gi && !await _db.InstructorProfiles.AnyAsync(p => p.Id == gi && p.StudioId == studioId, ct))
            throw new NotFoundException("Hoca", gi);
        return await ContractLineScheduleEngine.RunAsync(_db, _clock, studioId, userId, line, duration, request.Slots, request.ReuseExistingGroup, generate: true, ct,
            _equipmentGuard, request.InstructorProfileId, request.Capacity);
    }
}
