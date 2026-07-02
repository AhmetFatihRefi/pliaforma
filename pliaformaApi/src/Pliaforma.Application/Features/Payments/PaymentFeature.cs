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
    Guid? StudentContractId = null,
    // Sprint 28B: line bağı + etiket ("ClassTypeName · start–end"); line yoksa null → UI "Diğer ödeme".
    Guid? StudentContractLineId = null,
    string? LineLabel = null);

internal static class PaymentMapping
{
    /// <summary>Overdue DİNAMİK: depoda Pending + ödenmemiş + DueDate geçmiş → Overdue olarak gösterilir.</summary>
    public static PaymentStatus Effective(PaymentStatus stored, DateTime? paidAt, DateOnly dueDate, DateOnly today) =>
        stored == PaymentStatus.Pending && paidAt is null && dueDate < today ? PaymentStatus.Overdue : stored;

    public static string? LineLabel(string? classTypeName, DateOnly? start, DateOnly? end) =>
        classTypeName is null ? null : $"{classTypeName} · {start:yyyy-MM-dd} – {end:yyyy-MM-dd}";

    public static PaymentDto ToDto(Payment p, string studentName, DateOnly today, string? lineLabel = null) =>
        new(p.Id, p.StudentProfileId, studentName, p.StudentPackageId, p.Amount, p.Currency, p.DueDate,
            p.PaidAt, Effective(p.Status, p.PaidAt, p.DueDate, today), p.Method, p.Notes, p.CreatedAt,
            p.StudentContractId, p.StudentContractLineId, lineLabel);
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
            .Select(p => new
            {
                p, Name = p.StudentProfile!.FullName,
                LineCt = p.StudentContractLine!.ClassType!.Name,
                LineStart = (DateOnly?)p.StudentContractLine!.StartDate,
                LineEnd = (DateOnly?)p.StudentContractLine!.EndDate,
            })
            .ToListAsync(ct);
        return rows.Select(r => PaymentMapping.ToDto(r.p, r.Name, today,
            PaymentMapping.LineLabel(r.LineCt, r.LineStart, r.LineEnd))).ToList();
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
            .Select(p => new
            {
                p, Name = p.StudentProfile!.FullName,
                LineCt = p.StudentContractLine!.ClassType!.Name,
                LineStart = (DateOnly?)p.StudentContractLine!.StartDate,
                LineEnd = (DateOnly?)p.StudentContractLine!.EndDate,
            })
            .ToListAsync(ct);
        return rows.Select(r => PaymentMapping.ToDto(r.p, r.Name, today,
            PaymentMapping.LineLabel(r.LineCt, r.LineStart, r.LineEnd))).ToList();
    }
}

// ---- Oluştur ----
// Sprint 45: Payment = ALINAN para. Yeni kayıt doğrudan Paid (PaidAt=now); vade (DueDate) UI'dan alınmaz (opsiyonel; verilmezse today).
public sealed record CreatePaymentCommand(
    Guid StudentProfileId, Guid? StudentPackageId, decimal Amount, string? Currency, DateOnly? DueDate,
    PaymentMethod? Method, string? Notes, Guid? StudentContractId = null,
    Guid? StudentContractLineId = null) : ICommand<PaymentDto>, IStudioWriteRequest;

public sealed class CreatePaymentValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty();
        // Sprint 29A (strict payment): ödeme artık ZORUNLU olarak bir sözleşme satırına (line) bağlanır. Line yoksa 400.
        // Eski "Diğer ödeme" (line'sız) yeni ödemelerde üretilemez; eski unassigned kayıtlar salt-okunur korunur.
        RuleFor(x => x.StudentContractLineId).NotEmpty().WithMessage("Ödeme bir sözleşme satırına (paket) bağlanmalı.");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        // Sprint 45: DueDate zorunlu değil (alınan ödemenin vadesi yok); verilirse geçerli bir tarih olmalı.
        RuleFor(x => x.Currency).Length(3).When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class CreatePaymentHandler : ICommandHandler<CreatePaymentCommand, PaymentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ISecurityAuditLogger _audit;
    public CreatePaymentHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, ISecurityAuditLogger audit)
    { _db = db; _currentUser = currentUser; _clock = clock; _audit = audit; }

    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        if (request.StudentPackageId is { } pkgId
            && !await _db.StudentPackages.AnyAsync(p => p.Id == pkgId && p.StudioId == studioId && p.StudentProfileId == student.Id, ct))
            throw new NotFoundException("Öğrenci paketi", pkgId);

        // Sprint 28B: ödeme öncelikle LINE'a bağlanır. lineId verilirse line öğrenciye ait + iptal değil + currency uyumlu;
        // StudentContractId line'dan TÜRETİLİR. lineId yoksa eski contractId yolu tolere edilir (geri uyum). İkisi de yoksa
        // "Diğer ödeme" (borca sayılmaz). Etiket (label) my-payments'ta gösterilir.
        StudentContractLine? line = null;
        Guid? contractId = request.StudentContractId;
        string? lineLabel = null;
        string? bindCurrency = null;   // currency uyum referansı (line varsa line.Currency, yoksa contract.Currency)

        if (request.StudentContractLineId is { } lid)
        {
            line = await _db.StudentContractLines
                .Include(l => l.ClassType)
                .FirstOrDefaultAsync(l => l.Id == lid && l.StudioId == studioId && l.StudentContract!.StudentProfileId == student.Id, ct)
                ?? throw new NotFoundException("Sözleşme satırı", lid);
            if (line.Status == StudentContractLineStatus.Cancelled)
                throw new BusinessRuleException("İptal edilmiş satıra ödeme eklenemez.");
            contractId = line.StudentContractId;                  // türet
            bindCurrency = line.Currency;
            lineLabel = PaymentMapping.LineLabel(line.ClassType?.Name, line.StartDate, line.EndDate);
        }
        else if (contractId is { } cid)
        {
            var contract = await _db.StudentContracts
                .FirstOrDefaultAsync(c => c.Id == cid && c.StudioId == studioId && c.StudentProfileId == student.Id, ct)
                ?? throw new NotFoundException("Sözleşme", cid);
            bindCurrency = contract.Currency;
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? bindCurrency ?? await _db.Studios.Where(s => s.Id == studioId).Select(s => s.Currency).FirstAsync(ct)
            : request.Currency.Trim().ToUpperInvariant();

        if (bindCurrency is not null && !string.Equals(currency, bindCurrency, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["currency"] = new[] { $"Ödeme para birimi {(line is null ? "sözleşme" : "satır")} ile aynı olmalı ({bindCurrency})." },
            });

        // Sprint 45: kaydedilen ödeme = ALINAN para → doğrudan Paid (PaidAt=now). DueDate verilmezse today (bilgi amaçlı; vade kavramı yok).
        var payment = new Payment
        {
            StudioId = studioId,
            StudentProfileId = student.Id,
            StudentPackageId = request.StudentPackageId,
            StudentContractId = contractId,
            StudentContractLineId = line?.Id,
            Amount = request.Amount,
            Currency = currency,
            DueDate = request.DueDate ?? today,
            Status = PaymentStatus.Paid,
            PaidAt = _clock.UtcNow,
            Method = request.Method,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = userId,
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        // Denetim (31C-1): ödeme oluşturuldu — TUTAR yazılmaz.
        await _audit.LogAsync(SecurityAuditEventType.PaymentCreated, SecurityAuditOutcome.Success,
            actorUserId: userId, studioId: studioId, targetType: "Payment", targetId: payment.Id,
            cancellationToken: ct);

        return PaymentMapping.ToDto(payment, student.FullName, today, lineLabel);
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
    private readonly ISecurityAuditLogger _audit;
    public UpdatePaymentHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, ISecurityAuditLogger audit)
    { _db = db; _currentUser = currentUser; _clock = clock; _audit = audit; }

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

        // Denetim (31C-1): ödeme güncellendi — TUTAR yazılmaz.
        await _audit.LogAsync(SecurityAuditEventType.PaymentUpdated, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: studioId, targetType: "Payment", targetId: payment.Id,
            cancellationToken: ct);

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
    private readonly ISecurityAuditLogger _audit;
    public MarkPaymentPaidHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, ISecurityAuditLogger audit)
    { _db = db; _currentUser = currentUser; _clock = clock; _audit = audit; }

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

        // Denetim (31C-1): ödeme ödendi işaretlendi — TUTAR yazılmaz.
        await _audit.LogAsync(SecurityAuditEventType.PaymentMarkedPaid, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: studioId, targetType: "Payment", targetId: payment.Id,
            cancellationToken: ct);

        return Unit.Value;
    }
}

// ---- İptal ----
public sealed record CancelPaymentCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class CancelPaymentHandler : ICommandHandler<CancelPaymentCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecurityAuditLogger _audit;
    public CancelPaymentHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    { _db = db; _currentUser = currentUser; _audit = audit; }

    public async Task<Unit> Handle(CancelPaymentCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ödeme", request.Id);
        if (payment.Status == PaymentStatus.Cancelled)
            throw new ConflictException("Ödeme zaten iptal edilmiş.");

        payment.Status = PaymentStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        // Denetim (31C-1): ödeme iptal edildi.
        await _audit.LogAsync(SecurityAuditEventType.PaymentCancelled, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: studioId, targetType: "Payment", targetId: payment.Id,
            cancellationToken: ct);

        return Unit.Value;
    }
}
