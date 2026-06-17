using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir rezervasyonun (ClassBooking) yoklama kaydı: öğrencinin derse fiilen gelip gelmediği. **Booking
/// üzerinden çalışır** — booking olmayan öğrenci için yoklama tutulmaz. Her aktif booking için EN FAZLA
/// bir Attendance kaydı vardır (ikinci işaretleme update'tir). ClassSessionId/StudentProfileId booking
/// ile tutarlı olacak şekilde denormalize tutulur (sorgu kolaylığı). Tenant-scoped. Yoklama
/// ödeme/paket hakkı DÜŞÜRMEZ; yalnızca katılım kaydıdır.
/// </summary>
public class Attendance : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid ClassBookingId { get; set; }
    public ClassBooking? ClassBooking { get; set; }

    /// <summary>Booking ile tutarlı denormalize alan (seans bazlı sorgu için).</summary>
    public Guid ClassSessionId { get; set; }

    /// <summary>Booking ile tutarlı denormalize alan (öğrenci geçmişi için).</summary>
    public Guid StudentProfileId { get; set; }

    public AttendanceStatus Status { get; set; }

    public Guid MarkedByUserId { get; set; }

    /// <summary>Son işaretleme/değişiklik anı (UTC) — denetim için.</summary>
    public DateTime MarkedAt { get; set; }

    public string? Notes { get; set; }

    /// <summary>Bu yoklamadan doğan telafi kredisi hareketi (varsa). İkinci kez kredi vermeyi engeller.</summary>
    public Guid? MakeupCreditTransactionId { get; set; }
}
