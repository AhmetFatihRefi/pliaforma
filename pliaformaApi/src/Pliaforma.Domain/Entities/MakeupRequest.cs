using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Telafi dersi talebi (Sprint 27 — Instructor Makeup Request & Pending Schedule Blocks). Hoca, kendi aktif
/// sözleşmeli ve telafi kredisi olan öğrencisi için bir telafi dersi TALEBİ açar; talep Owner/StudioAdmin
/// onaylamadan AKTİF DERS SAYILMAZ. Pending talep gerçek bir <see cref="ClassSession"/> DEĞİLDİR — bu yüzden
/// attendance/confirm/booking yoktur; yalnız programda "Onay bekliyor" görünür, hoca+öğrenci saatini bloklar ve
/// krediyi REZERVE eder (ledger'a reserve hareketi YAZILMAZ — rezerve, aktif Pending talep sayısıyla örtük).
///
/// Approve → final değerlerle gerçek Makeup ClassSession (IsIndividual=true) + ClassBooking materyalize edilir;
/// kredi mevcut BookingCreator makeup yolundan Use(−1) ile gerçekten tüketilir (<see cref="ResultingClassSessionId"/>
/// / <see cref="ResultingBookingId"/> izi tutulur). Reject/Withdraw → kayıt AUDIT için saklanır (soft-delete YOK),
/// ledger'a dokunulmaz. <see cref="StartAt"/>/<see cref="EndAt"/> stüdyo-yerel duvar saatidir (ClassSession ile
/// aynı eksen, Unspecified). Plain referans alanları için FK YOK (ikinci cascade yollarından kaçınma). Tenant-scoped.
/// </summary>
public class MakeupRequest : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    /// <summary>Telafi yapılacak öğrenci (misafir dahil — StudentProfileId üzerinden).</summary>
    public Guid StudentProfileId { get; set; }

    /// <summary>Sprint 28A: talebin bağlı olduğu sözleşme SATIRI (line). 28A'da backfill ile doldurulur; 28C'de
    /// line bazlı zorunlu (telafi hakkı + tüketim o line'dan). NULL → eski/geçiş.</summary>
    public Guid? StudentContractLineId { get; set; }
    public StudentContractLine? StudentContractLine { get; set; }

    /// <summary>Talebi açan/atanan hocanın profili (= planlanan hoca). Instructor için token'dan ZORLANIR.</summary>
    public Guid RequestedInstructorProfileId { get; set; }

    /// <summary>Telafi dersinin tipi (süre kaynağı → EndAt = StartAt + ClassType.DefaultDurationMinutes).</summary>
    public Guid ClassTypeId { get; set; }

    /// <summary>Önerilen başlangıç (stüdyo-yerel duvar saati, Unspecified). Geçmişe talep yasak (StartAt &gt; studioNow).</summary>
    public DateTime StartAt { get; set; }

    /// <summary>StartAt + ClassType.DefaultDurationMinutes ile hesaplanıp yazılır.</summary>
    public DateTime EndAt { get; set; }

    public MakeupRequestStatus Status { get; set; } = MakeupRequestStatus.Pending;

    /// <summary>Hocanın açıklaması/talep notu.</summary>
    public string? Description { get; set; }

    /// <summary>Talebi oluşturan kullanıcı (instructor veya manager).</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>Onay/red kararını veren manager.</summary>
    public Guid? DecisionByUserId { get; set; }

    /// <summary>Karar zamanı (UTC instant).</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>Manager kararı notu (red gerekçesi vb.).</summary>
    public string? DecisionNote { get; set; }

    /// <summary>Approve sonrası materyalize edilen seans (audit izi).</summary>
    public Guid? ResultingClassSessionId { get; set; }

    /// <summary>Approve sonrası materyalize edilen rezervasyon (audit izi).</summary>
    public Guid? ResultingBookingId { get; set; }
}
