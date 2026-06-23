using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Güvenlik denetim olaylarını append-only SecurityAuditLog'a yazar.
/// <para><b>Best-effort:</b> yazım hatası çağıran akışı KIRMAZ (sunucu loguna düşer). Bu nedenle çağıran
/// handler, asıl durum değişikliğini (ör. FailedLoginCount) KENDİ SaveChanges'i ile kalıcılaştırdıktan
/// SONRA bunu çağırmalıdır.</para>
/// <para>Ip/UserAgent uygulama tarafından (HttpContext) doldurulur. <paramref name="actorEmail"/> ham verilir,
/// uygulama maskeler. Parola/token/secret/request-body ASLA yazılmaz.</para>
/// </summary>
public interface ISecurityAuditLogger
{
    Task LogAsync(
        SecurityAuditEventType eventType,
        SecurityAuditOutcome outcome,
        Guid? actorUserId = null,
        string? actorEmail = null,
        Guid? studioId = null,
        string? targetType = null,
        Guid? targetId = null,
        string? detail = null,
        CancellationToken cancellationToken = default);
}
