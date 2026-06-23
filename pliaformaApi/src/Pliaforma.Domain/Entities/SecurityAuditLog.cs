using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Güvenlik denetim kaydı — <b>append-only</b>. Bilinçli olarak <see cref="Common.BaseEntity"/> DEĞİLDİR:
/// böylece global soft-delete query filtresi ve AuditInterceptor (CreatedAt/UpdatedAt/soft-delete) bu
/// entity'ye dokunmaz. Yalnızca eklenir; normal uygulama akışında update/delete EDİLMEZ.
/// <para>KVKK: PII maskeli yazılır (<see cref="ActorEmailMasked"/>); parola/token/secret ve request body
/// ASLA saklanmaz. Ip/UserAgent güvenlik denetimi (meşru menfaat) için tutulur. Retention: 2 yıl (purge 31D).</para>
/// </summary>
public class SecurityAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Olayın gerçekleştiği an (UTC).</summary>
    public DateTime OccurredAt { get; set; }

    public SecurityAuditEventType EventType { get; set; }
    public SecurityAuditOutcome Outcome { get; set; }

    /// <summary>Olayı tetikleyen kullanıcı (varsa; ör. bilinmeyen e-posta ile login denemesinde null).</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Maskeli aktör e-postası (ör. "j***@e***.com"). Ham e-posta SAKLANMAZ.</summary>
    public string? ActorEmailMasked { get; set; }

    public Guid? StudioId { get; set; }

    /// <summary>Hedef nesne tipi (31C'de genişler; bu sprintte çoğunlukla null).</summary>
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Kısa, maskeli ek bilgi. Parola/token/secret/request-body ASLA yazılmaz.</summary>
    public string? Detail { get; set; }
}
