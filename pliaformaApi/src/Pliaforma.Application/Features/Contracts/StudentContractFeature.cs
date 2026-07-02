using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Contracts;

// ---------------------------------------------------------------------------------------------------
// Öğrenci sözleşmesi CRUD (Sprint 18a). Owner/StudioAdmin (controller guard). Tenant-scoped.
// ---------------------------------------------------------------------------------------------------

public sealed record CreateStudentContractCommand(
    Guid StudentProfileId,
    Guid ClassTypeId,
    Guid InstructorProfileId,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalLessonCount,
    int MakeupAllowance,
    decimal Price,
    string? Currency,
    bool IsIndividualDefault,
    string? Notes,
    // Sprint 29A: legacy/ilk sözleşme ucu — tercih slotları OPSIYONEL (verilirse ilk line'a yazılır; verilmezse
    // line slotsuz/geçiş kalır). Yeni "alım" (CreateStudentContractLine) ≥1 slot ZORUNLU; bu uçta zorunlu değil.
    IReadOnlyList<PreferredSlotInput>? PreferredSlots = null) : ICommand<StudentContractDto>, IStudioWriteRequest;

public sealed class CreateStudentContractValidator : AbstractValidator<CreateStudentContractCommand>
{
    public CreateStudentContractValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.InstructorProfileId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
        RuleFor(x => x.TotalLessonCount).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.MakeupAllowance).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).MaximumLength(8);
        RuleFor(x => x.Notes).MaximumLength(2000);
        // Slot verilirse iç-duplicate olamaz (boş = geçerli; bu uçta slot opsiyonel).
        RuleFor(x => x.PreferredSlots)
            .Must(s => !PreferredSlotRules.HasInternalDuplicates(s!)).When(x => x.PreferredSlots is { Count: > 0 })
            .WithMessage("Aynı gün ve saat tercihi tekrar edemez.");
    }
}

public sealed class CreateStudentContractHandler : ICommandHandler<CreateStudentContractCommand, StudentContractDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecurityAuditLogger _audit;

    public CreateStudentContractHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<StudentContractDto> Handle(CreateStudentContractCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);
        if (student.Status == StudentStatus.Inactive)
            throw new BusinessRuleException("Pasif öğrenci için sözleşme oluşturulamaz.");

        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.ClassTypeId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders grubu", request.ClassTypeId);

        var instructor = await _db.InstructorProfiles
            .FirstOrDefaultAsync(p => p.Id == request.InstructorProfileId && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Hoca", request.InstructorProfileId);

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "TRY" : request.Currency.Trim().ToUpperInvariant();

        var contract = new StudentContract
        {
            StudioId = studioId,
            StudentProfileId = student.Id,
            ClassTypeId = classType.Id,
            InstructorProfileId = instructor.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalLessonCount = request.TotalLessonCount,
            MakeupAllowance = request.MakeupAllowance,
            Price = request.Price,
            Currency = currency,
            IsIndividualDefault = request.IsIndividualDefault,
            Status = StudentContractStatus.Active,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = userId,
        };
        _db.StudentContracts.Add(contract);

        // Sprint 28A (line modeli): her sözleşme header'dır + 1 line. Hak/ücret/telafi line üzerinde takip edilir;
        // davranış değişmez (eski akış hâlâ contract alanlarını okur). Yeni alımlar 28B'de yeni line olur.
        var line = new StudentContractLine
        {
            StudioId = studioId,
            StudentContractId = contract.Id,
            ClassTypeId = classType.Id,
            InstructorProfileId = instructor.Id,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            LessonCount = contract.TotalLessonCount,
            PricePerLesson = contract.TotalLessonCount > 0 ? decimal.Round(contract.Price / contract.TotalLessonCount, 2) : 0m,
            TotalPrice = contract.Price,
            MakeupAllowance = contract.MakeupAllowance,
            DueDate = null,    // Karar: backfill/oluşturmada NULL; son ödeme tarihi line ekranında girilir (28B/28D).
            IsIndividual = contract.IsIndividualDefault,
            // Sprint 33: IsIndividual ↔ PreferredMaxCapacity senkron (bireysel → 1; grup → null = farketmez).
            PreferredMaxCapacity = contract.IsIndividualDefault ? 1 : null,
            Status = StudentContractLineStatus.Active,
            Currency = currency,
            SequenceNo = 1,
            CreatedByUserId = userId,
        };
        _db.StudentContractLines.Add(line);

        // Sprint 29A: bu uçta tercih slotları OPSIYONEL — verilirse ilk line'a yazılır (verilmezse slotsuz/geçiş).
        if (request.PreferredSlots is { Count: > 0 } prefs)
            foreach (var s in PreferredSlotRules.Build(studioId, line.Id, userId, prefs))
                _db.StudentContractLinePreferredSlots.Add(s);

        await _db.SaveChangesAsync(ct);

        // Denetim (31C-1): sözleşme oluşturuldu — FİYAT yazılmaz.
        await _audit.LogAsync(SecurityAuditEventType.ContractCreated, SecurityAuditOutcome.Success,
            actorUserId: userId, studioId: studioId, targetType: "StudentContract", targetId: contract.Id,
            cancellationToken: ct);

        return new StudentContractDto(
            contract.Id, student.Id, student.FullName, classType.Id, classType.Name,
            instructor.Id, instructor.DisplayName, contract.StartDate, contract.EndDate,
            contract.TotalLessonCount, contract.MakeupAllowance, contract.Price, contract.Currency,
            contract.IsIndividualDefault, contract.Status, contract.Notes, 0, contract.TotalLessonCount);
    }
}

// ---- Listele (bir öğrencinin sözleşmeleri) ----
// Sprint 48: sözleşme okuması (finans) → Removed/rol-düşürülen üye access token'ıyla okuyamaz.
public sealed record GetStudentContractsQuery(Guid StudentProfileId) : IQuery<IReadOnlyList<StudentContractDto>>, IStudioReadRequest;

public sealed class GetStudentContractsHandler : IQueryHandler<GetStudentContractsQuery, IReadOnlyList<StudentContractDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetStudentContractsHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<StudentContractDto>> Handle(GetStudentContractsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        return await ContractProjection.Query(_db, studioId)
            .Where(c => c.StudentProfileId == request.StudentProfileId)
            .OrderByDescending(c => c.StartDate)
            .Select(ContractProjection.ToDto(_db))
            .ToListAsync(ct);
    }
}

// ---- Tek sözleşme detayı ----
public sealed record GetStudentContractQuery(Guid Id) : IQuery<StudentContractDto>, IStudioReadRequest;

public sealed class GetStudentContractHandler : IQueryHandler<GetStudentContractQuery, StudentContractDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetStudentContractHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<StudentContractDto> Handle(GetStudentContractQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        return await ContractProjection.Query(_db, studioId)
            .Where(c => c.Id == request.Id)
            .Select(ContractProjection.ToDto(_db))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Sözleşme", request.Id);
    }
}

// ---- İptal ----
public sealed record CancelStudentContractCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class CancelStudentContractHandler : ICommandHandler<CancelStudentContractCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecurityAuditLogger _audit;
    public CancelStudentContractHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    { _db = db; _currentUser = currentUser; _audit = audit; }

    public async Task<Unit> Handle(CancelStudentContractCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var contract = await _db.StudentContracts
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme", request.Id);

        if (contract.Status == StudentContractStatus.Cancelled)
            return Unit.Value;

        contract.Status = StudentContractStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        // Denetim (31C-1): sözleşme iptal edildi.
        await _audit.LogAsync(SecurityAuditEventType.ContractCancelled, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: studioId, targetType: "StudentContract", targetId: contract.Id,
            cancellationToken: ct);

        return Unit.Value;
    }
}

/// <summary>Sözleşme DTO projeksiyonu — türetilen ScheduledLessonCount/AvailableLessonCount dahil.</summary>
internal static class ContractProjection
{
    // Sprint 47: gizli pilot header (IsPilot) hiçbir header listesi/detay yüzeyinde görünmez (line filtresiyle simetrik).
    public static IQueryable<StudentContract> Query(IAppDbContext db, Guid studioId) =>
        db.StudentContracts.AsNoTracking().Where(c => c.StudioId == studioId && !c.IsPilot);

    public static System.Linq.Expressions.Expression<Func<StudentContract, StudentContractDto>> ToDto(IAppDbContext db) =>
        c => new StudentContractDto(
            c.Id, c.StudentProfileId, c.StudentProfile!.FullName,
            c.ClassTypeId, c.ClassType!.Name, c.InstructorProfileId, c.InstructorProfile != null ? c.InstructorProfile.DisplayName : null,
            c.StartDate, c.EndDate, c.TotalLessonCount, c.MakeupAllowance, c.Price, c.Currency,
            c.IsIndividualDefault, c.Status, c.Notes,
            // ScheduledLessonCount: bu sözleşmeye bağlı aktif (Booked) Regular ders rezervasyonu.
            db.ClassBookings.Count(b => b.StudentContractId == c.Id
                && b.Status == ClassBookingStatus.Booked
                && b.ClassSession!.SessionKind == ClassSessionKind.Regular),
            // AvailableLessonCount = Total - scheduled (negatife düşmez).
            c.TotalLessonCount - db.ClassBookings.Count(b => b.StudentContractId == c.Id
                && b.Status == ClassBookingStatus.Booked
                && b.ClassSession!.SessionKind == ClassSessionKind.Regular));
}
