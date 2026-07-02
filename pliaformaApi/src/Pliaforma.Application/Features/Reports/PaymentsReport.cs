using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Contracts;
using Pliaforma.Application.Features.Dashboards;
using Pliaforma.Application.Features.Payments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Reports;

public sealed record PaymentsReportDto(string Month, PaymentTotalsDto Totals, IReadOnlyList<PaymentDto> Payments);

/// <summary>Aylık ödeme raporu (Owner/StudioAdmin). Sprint 45: liste = bu ay ALINAN (Paid) ödemeler;
/// Kalan/Gecikmiş toplamları LINE-bazlı (line.DueDate + remaining/overdue). Payment.Status filtresi kaldırıldı (Status param yok sayılır).</summary>
public sealed record GetPaymentsReportQuery(string? Month, PaymentStatus? Status = null) : IQuery<PaymentsReportDto>;

public sealed class GetPaymentsReportHandler : IQueryHandler<GetPaymentsReportQuery, PaymentsReportDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ISecurityAuditLogger _audit;
    public GetPaymentsReportHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, ISecurityAuditLogger audit)
    { _db = db; _currentUser = currentUser; _clock = clock; _audit = audit; }

    public async Task<PaymentsReportDto> Handle(GetPaymentsReportQuery request, CancellationToken ct)
    {
        var sid = _currentUser.RequireStudioId();
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, sid, ct);
        var today = DateOnly.FromDateTime(nowLocal);
        var p = ReportPeriod.Resolve(request.Month, nowLocal);

        // Sprint 45: liste = bu ay fiilen ALINAN (Paid) ödemeler (PaidAt). Payment.Status/DueDate tabanlı gecikme kavramı yok.
        var rows = await _db.Payments.AsNoTracking()
            .Where(pm => pm.StudioId == sid && pm.Status == PaymentStatus.Paid
                && pm.PaidAt != null && pm.PaidAt >= p.UtcStart && pm.PaidAt < p.UtcEnd)
            .OrderByDescending(pm => pm.PaidAt)
            .Select(pm => new { Payment = pm, Name = pm.StudentProfile!.FullName }).ToListAsync(ct);
        var dtos = rows.Select(r => PaymentMapping.ToDto(r.Payment, r.Name, today)).ToList();

        var paid = dtos.Sum(d => d.Amount);
        var paidC = dtos.Count;

        // Kalan/Gecikmiş = aktif line'ların anlık borç durumu (line.DueDate + remaining/overdue). Tek-currency toplar (N5).
        var debtLines = await ContractLineQuery.BuildAsync(_db,
            _db.StudentContractLines.AsNoTracking().Where(l => l.StudioId == sid && l.Status == StudentContractLineStatus.Active), today, ct);
        var overdue = debtLines.Sum(l => l.OverdueAmount);
        var overdueC = debtLines.Count(l => l.IsOverdue);
        var pending = debtLines.Sum(l => l.Remaining - l.OverdueAmount);
        var pendingC = debtLines.Count(l => l.Remaining > 0m && !l.IsOverdue);
        var totals = new PaymentTotalsDto(paid, pending, overdue, paidC, pendingC, overdueC);

        // Denetim (31C-1): toplu finansal rapor okundu — istek-başı tek event, yalnız sayı (TUTAR/PII yok).
        await _audit.LogAsync(SecurityAuditEventType.StudentDataListed, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: sid,
            targetType: "PaymentsReport", detail: $"count={dtos.Count}", cancellationToken: ct);

        return new PaymentsReportDto(p.Label, totals, dtos);
    }
}
