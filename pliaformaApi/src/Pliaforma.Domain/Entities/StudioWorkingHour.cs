using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Sprint 37 — Stüdyonun haftalık (tekrar eden) çalışma penceresi. Stüdyo başına haftanın 7 günü için en fazla bir
/// AKTİF kayıt. Kayıt yoksa runtime fallback 07:00–23:00 uygulanır (mevcut davranış). IsClosed=true → o gün kapalı
/// (OpensAt/ClosesAt null); açık günde OpensAt &lt; ClosesAt zorunlu. Gece aşan saat (OpensAt ≥ ClosesAt) kapsam dışı.
/// Tenant-scoped. Tekillik (StudioId, DayOfWeek) uygulama katmanında garanti edilir (full-replace + soft-delete).
/// </summary>
public class StudioWorkingHour : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    /// <summary>Haftanın günü (System.DayOfWeek: Sunday=0 .. Saturday=6). DB'de string saklanır.</summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Stüdyo-yerel açılış saati (duvar saati). IsClosed=true ise null.</summary>
    public TimeOnly? OpensAt { get; set; }

    /// <summary>Stüdyo-yerel kapanış saati (OpensAt'tan büyük olmalı). IsClosed=true ise null.</summary>
    public TimeOnly? ClosesAt { get; set; }

    /// <summary>true → o gün stüdyo kapalı (boşluk/ders/üretim yok).</summary>
    public bool IsClosed { get; set; }

    public bool IsActive { get; set; } = true;
}
