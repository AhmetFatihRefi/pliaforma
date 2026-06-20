using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Exceptions;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;
using ValidationException = Pliaforma.Application.Common.Exceptions.ValidationException;

namespace Pliaforma.Application.Features.Contracts;

// ---------------------------------------------------------------------------------------------------
// Sprint 23 — Sözleşme ek işlemi (amendment) + sözleşme-bazlı borç.
// Owner/StudioAdmin (controller guard). Amendment append-only: update/delete YOK.
// ---------------------------------------------------------------------------------------------------

/// <summary>
/// Mevcut aktif sözleşmeye ek ders hakkı / bitiş tarihi uzatma / ek ücret ekler. Tek atomik komut:
/// amendment satırı insert + contract.TotalLessonCount/EndDate mutasyonu (IStudioWriteRequest → tek transaction).
/// Program üretimi DAHİL DEĞİL — ayrı aksiyon (GenerateContractSchedule / GroupScheduleChange).
/// </summary>
public sealed record AddContractAmendmentCommand(
    Guid ContractId,
    ContractAmendmentKind Kind,
    int AdditionalLessonCount,
    DateOnly? NewEndDate,
    decimal AdditionalPrice,
    string? Currency,
    string? Description) : ICommand<ContractAmendmentDto>, IStudioWriteRequest;

public sealed class AddContractAmendmentValidator : AbstractValidator<AddContractAmendmentCommand>
{
    public AddContractAmendmentValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.AdditionalLessonCount).GreaterThanOrEqualTo(-1000).LessThanOrEqualTo(1000);
        RuleFor(x => x.AdditionalPrice).GreaterThanOrEqualTo(-1_000_000).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Currency).MaximumLength(8);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class AddContractAmendmentHandler : ICommandHandler<AddContractAmendmentCommand, ContractAmendmentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public AddContractAmendmentHandler(IAppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<ContractAmendmentDto> Handle(AddContractAmendmentCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var contract = await _db.StudentContracts
            .FirstOrDefaultAsync(c => c.Id == request.ContractId && c.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme", request.ContractId);

        // Yalnız aktif sözleşmeye ek işlem.
        if (contract.Status != StudentContractStatus.Active)
            throw new ConflictException("Yalnızca aktif sözleşmeye ek işlem yapılabilir.");

        // Para birimi sözleşme ile aynı olmalı (400).
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? contract.Currency
            : request.Currency.Trim().ToUpperInvariant();
        if (!string.Equals(currency, contract.Currency, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["currency"] = new[] { $"Para birimi sözleşme ile aynı olmalı ({contract.Currency})." },
            });

        // Bitiş tarihi yalnız ileri alınabilir; geri çekme yok.
        var newEndDate = request.NewEndDate ?? contract.EndDate;
        if (newEndDate < contract.EndDate)
            throw new BusinessRuleException("Bitiş tarihi yalnız ileri alınabilir, geri çekilemez.");

        // Ders hakkı, mevcut planlı (Booked Regular) ders sayısının altına indirilemez.
        var scheduled = await _db.ClassBookings.CountAsync(b => b.StudentContractId == contract.Id
            && b.Status == ClassBookingStatus.Booked
            && b.ClassSession!.SessionKind == ClassSessionKind.Regular, ct);
        var newTotal = contract.TotalLessonCount + request.AdditionalLessonCount;
        if (newTotal < scheduled)
            throw new ConflictException(
                $"Ders hakkı mevcut planlı ders sayısının ({scheduled}) altına indirilemez.");

        var amendment = new ContractAmendment
        {
            StudioId = studioId,
            StudentContractId = contract.Id,
            Kind = request.Kind,
            AdditionalLessonCount = request.AdditionalLessonCount,
            PreviousTotalLessonCount = contract.TotalLessonCount,
            NewTotalLessonCount = newTotal,
            PreviousEndDate = contract.EndDate,
            NewEndDate = newEndDate,
            AdditionalPrice = request.AdditionalPrice,
            Currency = currency,
            Description = request.Description?.Trim(),
            CreatedByUserId = userId,
        };
        _db.ContractAmendments.Add(amendment);

        // Sözleşmeyi canlı güncelle (AvailableLessonCount ve program üretimi bunları okur).
        contract.TotalLessonCount = newTotal;
        contract.EndDate = newEndDate;

        await _db.SaveChangesAsync(ct);

        var createdByName = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            ?? "—";
        return ToDto(amendment, createdByName);
    }

    internal static ContractAmendmentDto ToDto(ContractAmendment a, string createdByName) =>
        new(a.Id, a.StudentContractId, a.Kind, a.AdditionalLessonCount,
            a.PreviousTotalLessonCount, a.NewTotalLessonCount, a.PreviousEndDate, a.NewEndDate,
            a.AdditionalPrice, a.Currency, a.Description, createdByName, a.CreatedAt);
}

// ---- Amendment geçmişi (read-only) ----
public sealed record GetContractAmendmentsQuery(Guid ContractId) : IQuery<IReadOnlyList<ContractAmendmentDto>>;

public sealed class GetContractAmendmentsHandler : IQueryHandler<GetContractAmendmentsQuery, IReadOnlyList<ContractAmendmentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetContractAmendmentsHandler(IAppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<ContractAmendmentDto>> Handle(GetContractAmendmentsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        return await _db.ContractAmendments.AsNoTracking()
            .Where(a => a.StudioId == studioId && a.StudentContractId == request.ContractId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ContractAmendmentDto(
                a.Id, a.StudentContractId, a.Kind, a.AdditionalLessonCount,
                a.PreviousTotalLessonCount, a.NewTotalLessonCount, a.PreviousEndDate, a.NewEndDate,
                a.AdditionalPrice, a.Currency, a.Description,
                _db.Users.Where(u => u.Id == a.CreatedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "—",
                a.CreatedAt))
            .ToListAsync(ct);
    }
}

// ---- Öğrenci borç özeti (Sprint 28B: LINE-bazlı; amendment debt'e KATILMAZ) ----
public sealed record GetStudentDebtQuery(Guid StudentProfileId) : IQuery<StudentDebtSummaryDto>;

public sealed class GetStudentDebtHandler : IQueryHandler<GetStudentDebtQuery, StudentDebtSummaryDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetStudentDebtHandler(IAppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<StudentDebtSummaryDto> Handle(GetStudentDebtQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var d = await ContractLineQuery.ComputeDebtAsync(_db, studioId, request.StudentProfileId, ct);
        return new StudentDebtSummaryDto(d.ByCurrency, d.Lines, d.UnassignedByCurrency);
    }
}
