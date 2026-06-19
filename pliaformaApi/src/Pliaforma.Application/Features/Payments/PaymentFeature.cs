using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Exceptions;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;
using ValidationException = Pliaforma.Application.Common.Exceptions.ValidationException;

namespace Pliaforma.Application.Features.Payments;

public sealed record PaymentDto(
    Guid Id, Guid StudentProfileId, string StudentName, Guid? StudentPackageId,
    decimal Amount, string Currency, DateOnly DueDate, DateTime? PaidAt,
    PaymentStatus Status, PaymentMethod? Method, string? Notes, DateTime CreatedAt,
    Guid? StudentContractId = null);

internal static class PaymentMapping
{
    /// <summary>Overdue DİNAMİK: depoda Pending + ödenmemiş + DueDate geçmiş → Overdue olarak gösterilir.</summary>
    public static PaymentStatus Effective(PaymentStatus stored, DateTime? paidAt, DateOnly dueDate, DateOnly today) =>
        stored == PaymentStatus.Pending && paidAt is null && dueDate < today ? PaymentStatus.Overdue : stored;

    public static PaymentDto ToDto(Payment p, string studentName, DateOnly today) =>
        new(p.Id, p.StudentProfileId, studentName, p.StudentPackageId, p.Amount, p.Currency, p.DueDate,
            p.PaidAt, Effective(p.Status, p.PaidAt, p.DueDate, today), p.Method, p.Notes, p.CreatedAt,
            p.StudentContractId);
}

// ---- Öğrencinin ödemeleri (staff) ----
public sealed record GetStudentPaymentsQuery(Guid StudentProfileId) : IQuery<IReadOnlyList<PaymentDto>>;

public sealed class GetStudentPaymentsHandler : IQueryHandler<GetStudentPaymentsQuery, IReadOnlyList<PaymentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetStudentPaymentsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<IReadOnlyList<PaymentDto>> Handle(GetStudentPaymentsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var rows = await _db.Payments
            .Where(p => p.StudioId == studioId && p.StudentProfileId == request.StudentProfileId)
            .OrderByDescending(p => p.DueDate)
            .Select(p => new { p, Name = p.StudentProfile!.FullName })
            .ToListAsync(ct);
        return rows.Select(r => PaymentMapping.ToDto(r.p, r.Name, today)).ToList();
    }
}

// ---- Stüdyo ödeme listesi (filtreli) ----
public sealed record GetPaymentsQuery(Guid? StudentProfileId, PaymentStatus? Status) : IQuery<IReadOnlyList<PaymentDto>>;

public sealed class GetPaymentsHandler : IQueryHandler<GetPaymentsQuery, IReadOnlyList<PaymentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetPaymentsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<IReadOnlyList<PaymentDto>> Handle(GetPaymentsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));

        var query = _db.Payments.Where(p => p.StudioId == studioId);
        if (request.StudentProfileId is { } sid)
            query = query.Where(p => p.StudentProfileId == sid);

        // Overdue dinamik olduğundan SQL'de türetilir; diğer durumlar depo değeriyle eşleşir.
        if (request.Status is { } st)
        {
            query = st == PaymentStatus.Overdue
                ? query.Where(p => p.Status == PaymentStatus.Pending && p.PaidAt == null && p.DueDate < today)
                : query.Where(p => p.Status == st);
        }

        var rows = await query
            .OrderByDescending(p => p.DueDate)
            .Select(p => new { p, Name = p.StudentProfile!.FullName })
            .ToListAsync(ct);
        return rows.Select(r => PaymentMapping.ToDto(r.p, r.Name, today)).ToList();
    }
}

// ---- Oluştur ----
public sealed record CreatePaymentCommand(
    Guid StudentProfileId, Guid? StudentPackageId, decimal Amount, string? Currency, DateOnly DueDate,
    PaymentMethod? Method, string? Notes, Guid? StudentContractId = null) : ICommand<PaymentDto>, IStudioWriteRequest;

public sealed class CreatePaymentValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DueDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Currency).Length(3).When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class CreatePaymentHandler : ICommandHandler<CreatePaymentCommand, PaymentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public CreatePaymentHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        if (request.StudentPackageId is { } pkgId
            && !await _db.StudentPackages.AnyAsync(p => p.Id == pkgId && p.StudioId == studioId && p.StudentProfileId == student.Id, ct))
            throw new NotFoundException("Öğrenci paketi", pkgId);

        // Sprint 23: ödeme bir sözleşmeye bağlanıyorsa, sözleşme bu öğrenciye ait olmalı ve para birimi uyuşmalı.
        StudentContract? contract = null;
        if (request.StudentContractId is { } contractId)
        {
            contract = await _db.StudentContracts
                .FirstOrDefaultAsync(c => c.Id == contractId && c.StudioId == studioId && c.StudentProfileId == student.Id, ct)
                ?? throw new NotFoundException("Sözleşme", contractId);
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? contract?.Currency ?? await _db.Studios.Where(s => s.Id == studioId).Select(s => s.Currency).FirstAsync(ct)
            : request.Currency.Trim().ToUpperInvariant();

        if (contract is not null && !string.Equals(currency, contract.Currency, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["currency"] = new[] { $"Ödeme para birimi sözleşme ile aynı olmalı ({contract.Currency})." },
            });

        var payment = new Payment
        {
            StudioId = studioId,
            StudentProfileId = student.Id,
            StudentPackageId = request.StudentPackageId,
            StudentContractId = request.StudentContractId,
            Amount = request.Amount,
            Currency = currency,
            DueDate = request.DueDate,
            Status = PaymentStatus.Pending,
            Method = request.Method,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = userId,
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        return PaymentMapping.ToDto(payment, student.FullName, today);
    }
}

// ---- Güncelle ----
public sealed record UpdatePaymentCommand(Guid Id, decimal Amount, DateOnly DueDate, PaymentMethod? Method, string? Notes)
    : ICommand<PaymentDto>, IStudioWriteRequest;

public sealed class UpdatePaymentValidator : AbstractValidator<UpdatePaymentCommand>
{
    public UpdatePaymentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DueDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class UpdatePaymentHandler : ICommandHandler<UpdatePaymentCommand, PaymentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public UpdatePaymentHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<PaymentDto> Handle(UpdatePaymentCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ödeme", request.Id);
        if (payment.Status is PaymentStatus.Cancelled or PaymentStatus.Refunded)
            throw new ConflictException("İptal/iade edilmiş ödeme güncellenemez.");

        payment.Amount = request.Amount;
        payment.DueDate = request.DueDate;
        payment.Method = request.Method;
        payment.Notes = request.Notes?.Trim();
        await _db.SaveChangesAsync(ct);

        var name = await _db.StudentProfiles.Where(s => s.Id == payment.StudentProfileId).Select(s => s.FullName).FirstAsync(ct);
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        return PaymentMapping.ToDto(payment, name, today);
    }
}

// ---- Ödendi işaretle ----
public sealed record MarkPaymentPaidCommand(Guid Id, PaymentMethod? Method) : ICommand<Unit>, IStudioWriteRequest;

public sealed class MarkPaymentPaidHandler : ICommandHandler<MarkPaymentPaidCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public MarkPaymentPaidHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<Unit> Handle(MarkPaymentPaidCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ödeme", request.Id);
        if (payment.Status == PaymentStatus.Paid)
            throw new ConflictException("Ödeme zaten ödenmiş.");
        if (payment.Status is PaymentStatus.Cancelled or PaymentStatus.Refunded)
            throw new ConflictException("İptal/iade edilmiş ödeme ödendi işaretlenemez.");

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = _clock.UtcNow;
        if (request.Method is { } m) payment.Method = m;
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- İptal ----
public sealed record CancelPaymentCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class CancelPaymentHandler : ICommandHandler<CancelPaymentCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public CancelPaymentHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<Unit> Handle(CancelPaymentCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ödeme", request.Id);
        if (payment.Status == PaymentStatus.Cancelled)
            throw new ConflictException("Ödeme zaten iptal edilmiş.");

        payment.Status = PaymentStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
