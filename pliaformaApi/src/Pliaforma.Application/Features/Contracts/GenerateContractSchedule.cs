using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
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

    public async Task<ContractSchedulePreviewDto> Handle(PreviewContractScheduleQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var contract = await _db.StudentContracts.FirstOrDefaultAsync(c => c.Id == request.ContractId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme", request.ContractId);

        var duration = await _db.ClassTypes.Where(c => c.Id == contract.ClassTypeId).Select(c => c.DefaultDurationMinutes).FirstAsync(ct);
        var reserved = await _db.ClassBookings.CountAsync(b => b.StudentContractId == contract.Id
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular, ct);
        var available = Math.Max(0, contract.TotalLessonCount - reserved);

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);
        var closures = await _db.StudioClosures
            .Where(cl => cl.StudioId == studioId && cl.EndAt >= contract.StartDate.ToDateTime(TimeOnly.MinValue))
            .ToListAsync(ct);

        var candidates = ContractScheduleEngine.BuildCandidates(contract, duration, request.Slots);
        var slots = new List<ContractScheduleSlotPreviewDto>();
        var planned = 0;
        foreach (var c in candidates)
        {
            if (planned >= available) break;
            string? skip = null;
            if (c.StartAt <= nowLocal) skip = "Geçmiş zaman";
            else if (ContractScheduleEngine.InAnyClosure(c.StartAt, closures)) skip = "Stüdyo kapalı (tatil/kapanış)";
            else if (await SessionRules.HasConflictAsync(_db, studioId, contract.InstructorProfileId, c.StartAt, c.EndAt, null, ct)
                     && !await ExistingReusableGroupAsync(studioId, contract, c.StartAt, ct))
                skip = "Hoca çakışması";
            else if (await StudentBusyAsync(studioId, contract.StudentProfileId, c.StartAt, c.EndAt, ct))
                skip = "Öğrenci çakışması";

            var plannable = skip is null;
            slots.Add(new ContractScheduleSlotPreviewDto(c.StartAt, c.EndAt, plannable, skip));
            if (plannable) planned++;
        }

        return new ContractSchedulePreviewDto(available, planned, slots);
    }

    private Task<bool> ExistingReusableGroupAsync(Guid studioId, StudentContract contract, DateTime startAt, CancellationToken ct) =>
        _db.ClassSessions.AnyAsync(s => s.StudioId == studioId && s.InstructorProfileId == contract.InstructorProfileId
            && s.ClassTypeId == contract.ClassTypeId && s.StartAt == startAt
            && s.Status == ClassSessionStatus.Scheduled && !s.IsIndividual, ct);

    private Task<bool> StudentBusyAsync(Guid studioId, Guid studentProfileId, DateTime startAt, DateTime endAt, CancellationToken ct) =>
        _db.ClassBookings.AnyAsync(b => b.StudioId == studioId && b.StudentProfileId == studentProfileId
            && b.Status == ClassBookingStatus.Booked
            && b.ClassSession!.Status == ClassSessionStatus.Scheduled
            && b.ClassSession.StartAt < endAt && startAt < b.ClassSession.EndAt, ct);
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
    public GenerateContractScheduleHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<GenerateContractScheduleResultDto> Handle(GenerateContractScheduleCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var contract = await _db.StudentContracts.FirstOrDefaultAsync(c => c.Id == request.ContractId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme", request.ContractId);
        if (contract.Status != StudentContractStatus.Active)
            throw new BusinessRuleException("Yalnızca aktif sözleşmeden program üretilebilir.");

        var classType = await _db.ClassTypes.FirstAsync(c => c.Id == contract.ClassTypeId, ct);
        var duration = classType.DefaultDurationMinutes;
        var individual = request.Individual ?? contract.IsIndividualDefault;

        var reserved = await _db.ClassBookings.CountAsync(b => b.StudentContractId == contract.Id
            && b.Status == ClassBookingStatus.Booked && b.ClassSession!.SessionKind == ClassSessionKind.Regular, ct);
        var available = Math.Max(0, contract.TotalLessonCount - reserved);
        if (available == 0)
            return new GenerateContractScheduleResultDto(0, 0, 0, 0, Array.Empty<DateTime>());

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);
        var closures = await _db.StudioClosures.Where(cl => cl.StudioId == studioId).ToListAsync(ct);

        var candidates = ContractScheduleEngine.BuildCandidates(contract, duration, request.Slots);

        var created = new List<DateTime>();
        var newSessions = 0; var reusedSessions = 0; var skipped = 0;
        // Aynı batch içinde oluşturulanları çakışma için takip et (hoca/öğrenci meşguliyeti).
        var batchInstructor = new List<(DateTime s, DateTime e)>();
        var batchStudent = new List<(DateTime s, DateTime e)>();

        foreach (var c in candidates)
        {
            if (created.Count >= available) break;

            if (c.StartAt <= nowLocal) { skipped++; continue; }
            if (ContractScheduleEngine.InAnyClosure(c.StartAt, closures)) { skipped++; continue; }

            // Reuse: aynı slotta mevcut grup seansı (bireysel değil) varsa ona bağlan.
            ClassSession? target = null;
            if (!individual && request.ReuseExistingGroup)
            {
                target = await _db.ClassSessions.FirstOrDefaultAsync(s => s.StudioId == studioId
                    && s.InstructorProfileId == contract.InstructorProfileId && s.ClassTypeId == contract.ClassTypeId
                    && s.StartAt == c.StartAt && s.Status == ClassSessionStatus.Scheduled && !s.IsIndividual, ct);
            }

            // Çakışma: hoca (reuse hedefi yoksa) + öğrenci. Batch içi meşguliyet de kontrol edilir.
            var instructorBusy = batchInstructor.Any(x => x.s < c.EndAt && c.StartAt < x.e)
                || await SessionRules.HasConflictAsync(_db, studioId, contract.InstructorProfileId, c.StartAt, c.EndAt, null, ct);
            if (target is null && instructorBusy) { skipped++; continue; }

            var studentBusy = batchStudent.Any(x => x.s < c.EndAt && c.StartAt < x.e)
                || await _db.ClassBookings.AnyAsync(b => b.StudioId == studioId && b.StudentProfileId == contract.StudentProfileId
                    && b.Status == ClassBookingStatus.Booked && b.ClassSession!.Status == ClassSessionStatus.Scheduled
                    && b.ClassSession.StartAt < c.EndAt && c.StartAt < b.ClassSession.EndAt, ct);
            if (studentBusy) { skipped++; continue; }

            if (target is null)
            {
                target = new ClassSession
                {
                    StudioId = studioId,
                    InstructorProfileId = contract.InstructorProfileId,
                    ClassTypeId = contract.ClassTypeId,
                    StartAt = c.StartAt,
                    EndAt = c.EndAt,
                    Capacity = individual ? 1 : ContractScheduleEngine.GroupUnlimitedCapacity,
                    BookedCount = 0,
                    Status = ClassSessionStatus.Scheduled,
                    SessionKind = ClassSessionKind.Regular,
                    IsIndividual = individual,
                    CreatedByUserId = userId,
                };
                _db.ClassSessions.Add(target);
                newSessions++;
            }
            else
            {
                reusedSessions++;
            }

            target.BookedCount += 1;
            _db.ClassBookings.Add(new ClassBooking
            {
                StudioId = studioId,
                ClassSessionId = target.Id,
                StudentProfileId = contract.StudentProfileId,
                StudentContractId = contract.Id,
                Status = ClassBookingStatus.Booked,
                BookedByUserId = userId,
            });

            batchInstructor.Add((c.StartAt, c.EndAt));
            batchStudent.Add((c.StartAt, c.EndAt));
            created.Add(c.StartAt);
        }

        await _db.SaveChangesAsync(ct);
        return new GenerateContractScheduleResultDto(created.Count, newSessions, reusedSessions, skipped, created);
    }
}
