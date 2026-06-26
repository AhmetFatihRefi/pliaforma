using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Contracts;

// =====================================================================================================
// Sprint 38 — Sözleşme satırı ödeme planı (taksit/vade takibi).
//   - Plan = beklenen ödeme takvimi (installment satırları). Payment = fiilen alınan para (DEĞİŞMEZ).
//   - Payment installment'a BAĞLANMAZ; durum okuma anında HESAPLANIR: line ödemesi (line.Paid, legacy-aware)
//     en eski vadeden sırayla taksitlere dağıtılır. Legacy ödemeler otomatik plana akar.
//   - Peşin = 1 taksit (Amount=TotalPrice). Taksitli = 2+. Plan yoksa eski davranış (line.DueDate + payments).
//   - Σ taksit = line.TotalPrice; plan kaydında line.DueDate = en erken vade (28D reminder kod değişmeden çalışır).
//   - Yetki: Owner/StudioAdmin (controller). today == DueDate overdue DEĞİL; DueDate < today + eksik → overdue.
// =====================================================================================================

/// <summary>Taksit durumu (HESAPLANAN — DB'de tutulmaz).</summary>
public enum InstallmentStatus { Planned, PartiallyPaid, Paid, Overdue }

/// <summary>Plan türü (taksit sayısından TÜRETİLİR — ayrı kolon yok).</summary>
public enum PaymentPlanKind { None, Cash, Installment }

public sealed record InstallmentInput(DateOnly DueDate, decimal Amount, string? Notes);

public sealed record PaymentInstallmentDto(
    Guid Id, int SequenceNo, DateOnly DueDate, decimal Amount, decimal PaidAmount, InstallmentStatus Status, string? Notes);

public sealed record ContractLinePaymentPlanDto(
    Guid LineId,
    string Currency,
    bool IsConfigured,                 // installment satırı var mı (false → eski davranış)
    PaymentPlanKind PlanType,          // None / Cash (1) / Installment (2+)
    decimal TotalPlannedAmount,        // = Σ Amount = line.TotalPrice
    decimal TotalPaidAmount,           // = line.Paid (legacy-aware)
    decimal RemainingAmount,           // = max(0, TotalPrice − Paid)
    decimal OverdueAmount,             // Σ (Amount − PaidAmount) over geçmiş-vadeli eksik taksitler
    decimal OverpaidAmount,            // = max(0, Paid − TotalPrice) (yalnız bilgi; iade/credit YOK)
    DateOnly? NextDueDate,             // en erken eksik taksit vadesi
    decimal? NextDueAmount,            // o taksitin kalan tutarı
    int PaidCount, int PartialCount, int OverdueCount, int UpcomingCount,
    IReadOnlyList<PaymentInstallmentDto> Installments);

/// <summary>Taksit planı kuralları: doğrulama (Σ == TotalPrice, amount>0), inşa (DueDate asc + SequenceNo), allocation/durum.</summary>
internal static class PaymentInstallmentRules
{
    public static bool AllPositive(IReadOnlyList<InstallmentInput> items) => items.All(i => i.Amount > 0m);

    public static bool SumMatches(IReadOnlyList<InstallmentInput> items, decimal totalPrice) =>
        decimal.Round(items.Sum(i => i.Amount), 2) == decimal.Round(totalPrice, 2);

    /// <summary>Girdiden taksit varlıkları (DueDate artan + SequenceNo 1..N).</summary>
    public static IEnumerable<ContractLinePaymentInstallment> Build(
        Guid studioId, Guid lineId, Guid userId, IReadOnlyList<InstallmentInput> items)
    {
        var seq = 1;
        foreach (var i in items.OrderBy(x => x.DueDate))
            yield return new ContractLinePaymentInstallment
            {
                StudioId = studioId,
                StudentContractLineId = lineId,
                SequenceNo = seq++,
                DueDate = i.DueDate,
                Amount = decimal.Round(i.Amount, 2),
                Notes = i.Notes?.Trim(),
                IsActive = true,
                CreatedByUserId = userId,
            };
    }

    public static DateOnly EarliestDueDate(IReadOnlyList<InstallmentInput> items) => items.Min(i => i.DueDate);

    /// <summary>line.Paid (legacy-aware) tutarını taksitlere en eski vadeden dağıtıp DTO üretir.</summary>
    public static ContractLinePaymentPlanDto Compute(
        Guid lineId, string currency, decimal totalPrice, decimal paid, DateOnly today,
        IReadOnlyList<ContractLinePaymentInstallment> installments)
    {
        var remaining = Math.Max(0m, totalPrice - paid);
        if (installments.Count == 0)
            return new ContractLinePaymentPlanDto(lineId, currency, false, PaymentPlanKind.None,
                totalPrice, paid, remaining, 0m, Math.Max(0m, paid - totalPrice),
                null, null, 0, 0, 0, 0, Array.Empty<PaymentInstallmentDto>());

        var ordered = installments.OrderBy(i => i.DueDate).ThenBy(i => i.SequenceNo).ToList();
        var pool = paid;
        var dtos = new List<PaymentInstallmentDto>(ordered.Count);
        decimal overdueAmount = 0m;
        int paidCount = 0, partialCount = 0, overdueCount = 0, upcomingCount = 0;
        DateOnly? nextDue = null;
        decimal? nextDueAmount = null;

        foreach (var inst in ordered)
        {
            var alloc = Math.Min(inst.Amount, Math.Max(0m, pool));
            pool -= alloc;
            var owed = inst.Amount - alloc;

            InstallmentStatus status;
            if (owed <= 0m) { status = InstallmentStatus.Paid; paidCount++; }
            else if (inst.DueDate < today) { status = InstallmentStatus.Overdue; overdueCount++; overdueAmount += owed; }
            else if (alloc > 0m) { status = InstallmentStatus.PartiallyPaid; partialCount++; }
            else { status = InstallmentStatus.Planned; upcomingCount++; }

            if (owed > 0m && nextDue is null) { nextDue = inst.DueDate; nextDueAmount = owed; }

            dtos.Add(new PaymentInstallmentDto(inst.Id, inst.SequenceNo, inst.DueDate, inst.Amount, alloc, status, inst.Notes));
        }

        var planType = ordered.Count == 1 ? PaymentPlanKind.Cash : PaymentPlanKind.Installment;
        return new ContractLinePaymentPlanDto(lineId, currency, true, planType,
            totalPrice, paid, remaining, overdueAmount, Math.Max(0m, pool),
            nextDue, nextDueAmount, paidCount, partialCount, overdueCount, upcomingCount, dtos);
    }
}

/// <summary>Plan hesaplama servisi (line.Paid'i mevcut borç matematiğinden alır → tutarlılık).</summary>
internal static class PaymentInstallmentPlanService
{
    public static async Task<ContractLinePaymentPlanDto> ComputeAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, StudentContractLine line, CancellationToken ct)
    {
        // line.Paid: mevcut ContractLineQuery ile birebir (DirectPaid + tek-line-header legacy fallback).
        var lineDto = (await ContractLineQuery.BuildAsync(db,
            db.StudentContractLines.AsNoTracking().Where(l => l.Id == line.Id), ct))[0];

        var installments = await db.ContractLinePaymentInstallments.AsNoTracking()
            .Where(i => i.StudentContractLineId == line.Id && i.IsActive)
            .OrderBy(i => i.DueDate).ThenBy(i => i.SequenceNo)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(db, clock, studioId, ct));
        return PaymentInstallmentRules.Compute(line.Id, line.Currency, line.TotalPrice, lineDto.Paid, today, installments);
    }
}

// ===================================================================================================
// GET /api/studio/contract-lines/{lineId}/payment-plan — hesaplanmış plan.
// ===================================================================================================
public sealed record GetContractLinePaymentPlanQuery(Guid LineId) : IQuery<ContractLinePaymentPlanDto>;

public sealed class GetContractLinePaymentPlanHandler : IQueryHandler<GetContractLinePaymentPlanQuery, ContractLinePaymentPlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetContractLinePaymentPlanHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<ContractLinePaymentPlanDto> Handle(GetContractLinePaymentPlanQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var line = await _db.StudentContractLines.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LineId && l.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme satırı", request.LineId);
        return await PaymentInstallmentPlanService.ComputeAsync(_db, _clock, studioId, line, ct);
    }
}

// ===================================================================================================
// PUT /api/studio/contract-lines/{lineId}/payment-plan — full replace.
// ===================================================================================================
public sealed record UpdateContractLinePaymentPlanCommand(Guid LineId, IReadOnlyList<InstallmentInput> Installments)
    : ICommand<ContractLinePaymentPlanDto>, IStudioWriteRequest;

public sealed class UpdateContractLinePaymentPlanValidator : AbstractValidator<UpdateContractLinePaymentPlanCommand>
{
    public UpdateContractLinePaymentPlanValidator()
    {
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.Installments).NotEmpty().WithMessage("En az bir taksit girilmeli.");
        RuleFor(x => x.Installments)
            .Must(PaymentInstallmentRules.AllPositive).When(x => x.Installments is { Count: > 0 })
            .WithMessage("Taksit tutarı 0'dan büyük olmalı.");
        RuleForEach(x => x.Installments).ChildRules(i =>
            i.RuleFor(z => z.Notes).MaximumLength(500));
    }
}

public sealed class UpdateContractLinePaymentPlanHandler : ICommandHandler<UpdateContractLinePaymentPlanCommand, ContractLinePaymentPlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public UpdateContractLinePaymentPlanHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<ContractLinePaymentPlanDto> Handle(UpdateContractLinePaymentPlanCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var line = await _db.StudentContractLines.FirstOrDefaultAsync(l => l.Id == request.LineId && l.StudioId == studioId, ct)
            ?? throw new NotFoundException("Sözleşme satırı", request.LineId);
        if (line.Status == StudentContractLineStatus.Cancelled)
            throw new ConflictException("İptal edilmiş satıra ödeme planı tanımlanamaz.");

        // Σ taksit = line.TotalPrice (2 ondalık). Aksi halde 400 (Application ValidationException → 400).
        if (!PaymentInstallmentRules.SumMatches(request.Installments, line.TotalPrice))
            throw new Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["installments"] = new[] { $"Taksit toplamı sözleşme tutarına eşit olmalı ({decimal.Round(line.TotalPrice, 2)} {line.Currency})." },
            });

        // FULL REPLACE: mevcut aktif taksitleri soft-delete + yenilerini ekle (tek transaction — TransactionBehavior).
        var existing = await _db.ContractLinePaymentInstallments
            .Where(i => i.StudentContractLineId == line.Id).ToListAsync(ct);
        _db.ContractLinePaymentInstallments.RemoveRange(existing); // AuditInterceptor → soft-delete

        foreach (var inst in PaymentInstallmentRules.Build(studioId, line.Id, userId, request.Installments))
            _db.ContractLinePaymentInstallments.Add(inst);

        // Sprint 38: 28D reminder uyumu — line.DueDate = en erken taksit vadesi.
        line.DueDate = PaymentInstallmentRules.EarliestDueDate(request.Installments);

        await _db.SaveChangesAsync(ct);

        var fresh = await _db.StudentContractLines.AsNoTracking().FirstAsync(l => l.Id == line.Id, ct);
        return await PaymentInstallmentPlanService.ComputeAsync(_db, _clock, studioId, fresh, ct);
    }
}
