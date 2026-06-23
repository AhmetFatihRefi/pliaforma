using Microsoft.AspNetCore.Http;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Infrastructure.Services;

/// <summary>
/// <see cref="ISecurityAuditLogger"/> uygulaması. HttpContext'ten Ip/UserAgent zenginleştirir, e-postayı
/// maskeler, denetim kaydını oluşturur ve <see cref="ISecurityAuditSink"/>'e verir — sink kaydı BAĞIMSIZ bir
/// bağlam/işlemde yazar (çağıran komutun transaction'ı geri alınsa bile kalıcı). Parola/token/secret yazılmaz.
/// </summary>
public sealed class SecurityAuditLogger : ISecurityAuditLogger
{
    private readonly ISecurityAuditSink _sink;
    private readonly IHttpContextAccessor _http;
    private readonly IDateTime _clock;

    public SecurityAuditLogger(ISecurityAuditSink sink, IHttpContextAccessor http, IDateTime clock)
    {
        _sink = sink;
        _http = http;
        _clock = clock;
    }

    public Task LogAsync(
        SecurityAuditEventType eventType,
        SecurityAuditOutcome outcome,
        Guid? actorUserId = null,
        string? actorEmail = null,
        Guid? studioId = null,
        string? targetType = null,
        Guid? targetId = null,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = _http.HttpContext;
        var ip = ctx?.Connection.RemoteIpAddress?.ToString();

        var ua = ctx?.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(ua))
            ua = null;
        else if (ua.Length > 400)
            ua = ua[..400];

        var entry = new SecurityAuditLog
        {
            OccurredAt = _clock.UtcNow,
            EventType = eventType,
            Outcome = outcome,
            ActorUserId = actorUserId,
            ActorEmailMasked = PiiMask.Email(actorEmail),
            StudioId = studioId,
            TargetType = targetType,
            TargetId = targetId,
            Ip = ip,
            UserAgent = ua,
            Detail = detail,
        };

        return _sink.WriteAsync(entry, cancellationToken);
    }
}
