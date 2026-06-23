using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Dashboards;
using Pliaforma.Application.Features.Payments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Reports;

public sealed record PaymentsReportDto(string Month, PaymentTotalsDto Totals, IReadOnlyList<PaymentDto> Payments);

/// <summary>Aylık ödeme raporu (Owner/StudioAdmin). DueDate bu ay; Overdue dinamik; opsiyonel durum filtresi.</summary>
public sealed record GetPaymentsReportQuery(string? Month, PaymentStatus? Status) : IQuery<PaymentsReportDto>;

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

        var rows = await _db.Payments.AsNoTracking()
            .Where(pm => pm.StudioId == sid && pm.DueDate >= p.StartDate && pm.DueDate < p.EndDate)
            .OrderByDescending(pm => pm.DueDate)
            .Select(pm => new { Payment = pm, Name = pm.StudentProfile!.FullName }).ToListAsync(ct);

        var dtos = rows.Select(r => PaymentMapping.ToDto(r.Payment, r.Name, today)).ToList();
        // Opsiyonel durum filtresi effective status üzerinden.
        if (request.Status is { } st)
            dtos = dtos.Where(d => d.Status == st).ToList();

        decimal paid = 0, pending = 0, overdue = 0; int paidC = 0, pendingC = 0, overdueC = 0;
        foreach (var d in dtos)
        {
            switch (d.Status)
            {
                case PaymentStatus.Paid: paid += d.Amount; paidC++; break;
                case PaymentStatus.Overdue: overdue += d.Amount; overdueC++; break;
                case PaymentStatus.Pending: pending += d.Amount; pendingC++; break;
            }
        }
        var totals = new PaymentTotalsDto(paid, pending, overdue, paidC, pendingC, overdueC);

        // Denetim (31C-1): toplu finansal rapor okundu — istek-başı tek event, yalnız sayı (TUTAR/PII yok).
        await _audit.LogAsync(SecurityAuditEventType.StudentDataListed, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: sid,
            targetType: "PaymentsReport", detail: $"count={dtos.Count}", cancellationToken: ct);

        return new PaymentsReportDto(p.Label, totals, dtos);
    }
}
