using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Equipments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Contracts;

/// <summary>
/// Sözleşmeden program üretimi (Sprint 18a). Sözleşmenin tarih aralığında, verilen gün/saat slotlarını
/// kullanılabilir ders hakkı (Total − rezerve) kadar doldurur. Kapanış günleri + geçmiş + çakışma (hoca/
/// öğrenci) atlanır. <c>reuseExistingGroup</c> ile aynı slottaki mevcut grup seansına bağlanır (yeni seans
/// açmaz); yoksa yeni seans açar. Bireysel ise her ders ayrı (Capacity=1), grup ise sınırsız (yüksek Capacity).
/// </summary>
internal static class ContractScheduleEngine
{
    public const int GroupUnlimitedCapacity = 9999; // grup için pratikte sınırsız

    public sealed record Candidate(DateTime StartAt, DateTime EndAt);

    /// <summary>Sözleşme + slotlardan kronolojik aday ders listesi üretir (oluşturma yok).</summary>
    public static List<Candidate> BuildCandidates(StudentContract contract, int durationMin, IEnumerable<ContractScheduleSlotInput> slots)
    {
        var list = new List<Candidate>();
        var bySlotDay = slots.ToList();
        for (var date = contract.StartDate; date <= contract.EndDate; date = date.AddDays(1))
        {
            foreach (var slot in bySlotDay.Where(s => s.DayOfWeek == date.DayOfWeek))
            {
                var start = date.ToDateTime(slot.StartTime, DateTimeKind.Unspecified);
                list.Add(new Candidate(start, start.AddMinutes(durationMin)));
            }
        }
        return list.OrderBy(c => c.StartAt).ToList();
    }

    public static bool InAnyClosure(DateTime startAt, List<StudioClosure> closures) =>
        closures.Any(cl => cl.StartAt <= startAt && startAt < cl.EndAt);

    // ---- Sprint 22: program üretiminin paylaşılan çekirdeği (generate + grup-değişikliği ortak) ----
    /// <summary>Aynı slotta yeniden kullanılabilir mevcut GRUP seansı (bireysel değil, Scheduled). Sprint 33: yalnız
    /// kapasitesi <paramref name="maxCapacity"/>'yi aşmayan gruplar (kapasite tercihi). Yoksa null.</summary>
    public static Task<ClassSession?> FindReusableGroupAsync(
        IAppDbContext db, Guid studioId, Guid instructorProfileId, Guid classTypeId, DateTime startAt, CancellationToken ct, int maxCapacity = int.MaxValue) =>
        db.ClassSessions.FirstOrDefaultAsync(s => s.StudioId == studioId
            && s.InstructorProfileId == instructorProfileId && s.ClassTypeId == classTypeId
            && s.StartAt == startAt && s.Status == ClassSessionStatus.Scheduled && !s.IsIndividual
            && s.Capacity <= maxCapacity, ct);

    /// <summary>Öğrencinin verilen aralıkta örtüşen aktif (Booked, Scheduled) bir dersi var mı?</summary>
    public static Task<bool> StudentBusyAsync(
        IAppDbContext db, Guid studioId, Guid studentProfileId, DateTime startAt, DateTime endAt, CancellationToken ct) =>
        db.ClassBookings.AnyAsync(b => b.StudioId == studioId && b.StudentProfileId == studentProfileId
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.Status == ClassSessionStatus.Scheduled
            && b.ClassSession.StartAt < endAt && startAt < b.ClassSession.EndAt, ct);

    /// <summary>Sözleşmenin rezerve (Booked + Regular) ders sayısı. AvailableLesson = Total − bu sayı.</summary>
    public static Task<int> ReservedRegularAsync(IAppDbContext db, Guid contractId, CancellationToken ct) =>
        db.ClassBookings.CountAsync(b => b.StudentContractId == contractId
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular, ct);

    /// <summary>Sprint 33: kapasite EXPLICIT verilir (effective). IsIndividual = (capacity == 1).</summary>
    public static ClassSession NewSession(
        Guid studioId, Guid instructorProfileId, Guid classTypeId, DateTime startAt, DateTime endAt, int capacity, Guid userId) =>
        new()
        {
            StudioId = studioId,
            InstructorProfileId = instructorProfileId,
            ClassTypeId = classTypeId,
            StartAt = startAt,
            EndAt = endAt,
            Capacity = capacity,
            BookedCount = 0,
            Status = ClassSessionStatus.Scheduled,
            SessionKind = ClassSessionKind.Regular,
            IsIndividual = capacity == 1,
            CreatedByUserId = userId,
        };

    public static ClassBooking NewBooking(Guid studioId, Guid sessionId, Guid studentProfileId, Guid contractId, Guid userId) =>
        new()
        {
            StudioId = studioId,
            ClassSessionId = sessionId,
            StudentProfileId = studentProfileId,
            StudentContractId = contractId,
            Status = ClassBookingStatus.Booked,
            BookedByUserId = userId,
        };
}

// ======================= ÖNİZLEME (oluşturma yok) =======================
public sealed record PreviewContractScheduleQuery(Guid ContractId, IReadOnlyList<ContractScheduleSlotInput> Slots)
    : IQuery<ContractSchedulePreviewDto>;

public sealed class PreviewContractScheduleHandler : IQueryHandler<PreviewContractScheduleQuery, ContractSchedulePreviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public PreviewContractScheduleHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    // Sprint 28C-1: DEPRECATED wrapper — header'ın aktif LINE'ları üzerinde line-bazlı önizleme üretip toplar (geri uyum).
    public async Task<ContractSchedulePreviewDto> Handle(PreviewContractScheduleQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var contract = await _db.StudentContracts.FirstOrDefaultAsync(c => c.Id == request.ContractId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme", request.ContractId);

        var lines = await _db.StudentContractLines
            .Include(l => l.StudentContract)
            .Where(l => l.StudioId == studioId && l.StudentContractId == contract.Id && l.Status == StudentContractLineStatus.Active)
            .OrderBy(l => l.SequenceNo).ToListAsync(ct);

        int totalAvailable = 0, totalPlanned = 0;
        var slots = new List<ContractScheduleSlotPreviewDto>();
        foreach (var line in lines)
        {
            var inner = await new PreviewLineScheduleHandler(_db, _currentUser, _clock)
                .Handle(new PreviewLineScheduleQuery(line.Id, request.Slots), ct);
            totalAvailable += inner.AvailableLessonCount;
            totalPlanned += inner.PlannableCount;
            slots.AddRange(inner.Slots);
        }
        return new ContractSchedulePreviewDto(totalAvailable, totalPlanned, slots);
    }
}

// ======================= ÜRETİM (oluşturur) =======================
public sealed record GenerateContractScheduleCommand(
    Guid ContractId,
    IReadOnlyList<ContractScheduleSlotInput> Slots,
    bool ReuseExistingGroup,
    bool? Individual) : ICommand<GenerateContractScheduleResultDto>, IStudioWriteRequest;

public sealed class GenerateContractScheduleValidator : AbstractValidator<GenerateContractScheduleCommand>
{
    public GenerateContractScheduleValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.Slots).NotEmpty().WithMessage("En az bir gün/saat seçilmeli.");
    }
}

public sealed class GenerateContractScheduleHandler : ICommandHandler<GenerateContractScheduleCommand, GenerateContractScheduleResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly EquipmentCapacityGuard _equipmentGuard;
    public GenerateContractScheduleHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, EquipmentCapacityGuard equipmentGuard)
    { _db = db; _currentUser = currentUser; _clock = clock; _equipmentGuard = equipmentGuard; }

    // Sprint 28C-1: DEPRECATED wrapper — header'ın aktif LINE'ları üzerinde line-bazlı üretim (booking lineId yazar). Geri uyum.
    public async Task<GenerateContractScheduleResultDto> Handle(GenerateContractScheduleCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var contract = await _db.StudentContracts.FirstOrDefaultAsync(c => c.Id == request.ContractId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme", request.ContractId);
        if (contract.Status != StudentContractStatus.Active)
            throw new BusinessRuleException("Yalnızca aktif sözleşmeden program üretilebilir.");

        var lines = await _db.StudentContractLines
            .Include(l => l.StudentContract)
            .Where(l => l.StudioId == studioId && l.StudentContractId == contract.Id && l.Status == StudentContractLineStatus.Active)
            .OrderBy(l => l.SequenceNo).ToListAsync(ct);

        int created = 0, newSessions = 0, reusedSessions = 0, skipped = 0;
        var startAts = new List<DateTime>();
        foreach (var line in lines)
        {
            var duration = await _db.ClassTypes.Where(c => c.Id == line.ClassTypeId).Select(c => c.DefaultDurationMinutes).FirstAsync(ct);
            var r = await ContractLineScheduleEngine.RunAsync(_db, _clock, studioId, userId, line, duration, request.Slots, request.ReuseExistingGroup, generate: true, ct, _equipmentGuard);
            created += r.CreatedCount; newSessions += r.NewSessionCount; reusedSessions += r.ReusedSessionCount; skipped += r.SkippedCount;
            startAts.AddRange(r.CreatedStartAts);
        }
        return new GenerateContractScheduleResultDto(created, newSessions, reusedSessions, skipped, startAts);
    }
}
