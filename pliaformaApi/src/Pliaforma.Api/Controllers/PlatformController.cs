using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Security;
using Pliaforma.Application.Features.Platform;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Platform (cross-tenant) yönetim uçları. YALNIZCA PlatformAdmin (global_role) erişebilir; aktif stüdyo
/// bağlamı GEREKMEZ. Cross-tenant veri yalnızca burada açıktır — normal tenant uçları PlatformAdmin'i otomatik
/// bypass ETMEZ. Standard/Owner/StudioAdmin/Instructor/Student bu uçlara erişemez (403).
/// </summary>
[Authorize(Policy = AuthPolicies.PlatformAdmin)]
[Route("api/platform")]
public sealed class PlatformController : ApiControllerBase
{
    public PlatformController(IDispatcher dispatcher) : base(dispatcher) { }

    // ---- Overview ----
    [HttpGet("overview")]
    public Task<IActionResult> Overview(CancellationToken ct) =>
        Send(new GetPlatformOverviewQuery(), ct: ct);

    // ---- Studios ----
    [HttpGet("studios")]
    public Task<IActionResult> Studios([FromQuery] string? search, [FromQuery] StudioStatus? status, CancellationToken ct) =>
        Send(new GetPlatformStudiosQuery(search, status), ct: ct);

    [HttpGet("studios/{id:guid}")]
    public Task<IActionResult> StudioDetail(Guid id, CancellationToken ct) =>
        Send(new GetPlatformStudioDetailQuery(id), ct: ct);

    [HttpPost("studios/{id:guid}/suspend")]
    public Task<IActionResult> Suspend(Guid id, [FromBody] SuspendStudioRequest body, CancellationToken ct) =>
        Send(new SuspendStudioCommand(id, body.Reason), ct: ct);

    [HttpPost("studios/{id:guid}/reactivate")]
    public Task<IActionResult> Reactivate(Guid id, CancellationToken ct) =>
        Send(new ReactivateStudioCommand(id), ct: ct);

    // ---- Plan kataloğu ----
    [HttpGet("plans")]
    public Task<IActionResult> Plans([FromQuery] bool onlyActive, CancellationToken ct) =>
        Send(new GetSubscriptionPlansQuery(onlyActive), ct: ct);

    [HttpPost("plans")]
    public Task<IActionResult> CreatePlan([FromBody] SubscriptionPlanInput body, CancellationToken ct) =>
        Send(new CreateSubscriptionPlanCommand(body), ct: ct);

    [HttpPut("plans/{id:guid}")]
    public Task<IActionResult> UpdatePlan(Guid id, [FromBody] SubscriptionPlanInput body, CancellationToken ct) =>
        Send(new UpdateSubscriptionPlanCommand(id, body), ct: ct);

    [HttpDelete("plans/{id:guid}")]
    public Task<IActionResult> DeletePlan(Guid id, CancellationToken ct) =>
        Send(new DeleteSubscriptionPlanCommand(id), ct: ct);

    // ---- Subscriptions ----
    [HttpGet("studios/{id:guid}/subscription")]
    public Task<IActionResult> GetSubscription(Guid id, CancellationToken ct) =>
        Send(new GetStudioSubscriptionQuery(id), ct: ct);

    [HttpPut("studios/{id:guid}/subscription")]
    public Task<IActionResult> UpdateSubscription(Guid id, [FromBody] UpdateSubscriptionRequest body, CancellationToken ct) =>
        Send(new UpdateStudioSubscriptionCommand(
            id, body.PlanName, body.Tier, body.Status, body.MonthlyPrice, body.Currency, body.BillingPeriod,
            body.CurrentPeriodStart, body.CurrentPeriodEnd, body.TrialEndsAt, body.Notes), ct: ct);

    [HttpPut("studios/{id:guid}/subscription/plan")]
    public Task<IActionResult> AssignPlan(Guid id, [FromBody] AssignPlanRequest body, CancellationToken ct) =>
        Send(new AssignSubscriptionPlanCommand(id, body.PlanId), ct: ct);

    [HttpPost("studios/{id:guid}/subscription/record-payment")]
    public Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest body, CancellationToken ct) =>
        Send(new RecordSubscriptionPaymentCommand(id, body.Amount, body.Notes), ct: ct);

    [HttpGet("studios/{id:guid}/subscription/payments")]
    public Task<IActionResult> SubscriptionPayments(Guid id, CancellationToken ct) =>
        Send(new GetSubscriptionPaymentsQuery(id), ct: ct);

    [HttpPost("studios/{id:guid}/subscription/cancel")]
    public Task<IActionResult> CancelSubscription(Guid id, CancellationToken ct) =>
        Send(new CancelStudioSubscriptionCommand(id), ct: ct);

    // ---- Health / observability ----
    [HttpGet("health-summary")]
    public Task<IActionResult> HealthSummary(CancellationToken ct) =>
        Send(new GetHealthSummaryQuery(), ct: ct);

    [HttpGet("recent-activity")]
    public Task<IActionResult> RecentActivity(CancellationToken ct) =>
        Send(new GetPlatformRecentActivityQuery(), ct: ct);

    // ---- Güvenlik denetim görüntüleyici (Sprint 31C-1; salt-okunur, cross-tenant, export YOK) ----
    [HttpGet("security-audit")]
    public Task<IActionResult> SecurityAudit(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] SecurityAuditEventType? eventType,
        [FromQuery] Guid? actorUserId,
        [FromQuery] Guid? studioId,
        [FromQuery] SecurityAuditOutcome? outcome,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Send(new GetSecurityAuditLogsQuery(from, to, eventType, actorUserId, studioId, outcome, page, pageSize), ct: ct);
}

public sealed record SuspendStudioRequest(string Reason);
public sealed record AssignPlanRequest(Guid PlanId);
public sealed record RecordPaymentRequest(decimal? Amount, string? Notes);

public sealed record UpdateSubscriptionRequest(
    string PlanName,
    string? Tier,
    SubscriptionStatus Status,
    decimal MonthlyPrice,
    string Currency,
    BillingPeriod BillingPeriod,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd,
    DateOnly? TrialEndsAt,
    string? Notes);
