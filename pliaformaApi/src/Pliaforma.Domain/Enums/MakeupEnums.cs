namespace Pliaforma.Domain.Enums;

/// <summary>
/// Ders seansının türü. Varsayılan Regular. Makeup (telafi) seansına rezervasyon, öğrencinin telafi
/// kredisini tüketir. Trial/Special bu sürümde yalnızca etikettir (özel davranış yoktur).
/// </summary>
public enum ClassSessionKind
{
    /// <summary>Normal ders. Rezervasyon kredi tüketmez.</summary>
    Regular = 0,

    /// <summary>Telafi dersi. Rezervasyon 1 telafi kredisi tüketir.</summary>
    Makeup = 1,

    /// <summary>Deneme dersi (şimdilik yalnızca etiket).</summary>
    Trial = 2,

    /// <summary>Özel/etkinlik dersi (şimdilik yalnızca etiket).</summary>
    Special = 3,
}

/// <summary>
/// Telafi kredisi defter (ledger) hareketinin türü. Bakiye = aktif hareketlerin işaretli toplamı.
/// Granted/Restored pozitif; Used/Revoked/Expired negatif.
/// </summary>
public enum MakeupCreditTransactionType
{
    /// <summary>Kredi verildi (+).</summary>
    Granted = 0,

    /// <summary>Kredi telafi rezervasyonunda kullanıldı (−).</summary>
    Used = 1,

    /// <summary>Kullanılmış kredi iptal sonrası geri verildi (+).</summary>
    Restored = 2,

    /// <summary>Verilmiş kredi geri alındı (−).</summary>
    Revoked = 3,

    /// <summary>Süresi dolduğu için düşüldü (−). Bu sürümde otomatik üretilmez.</summary>
    Expired = 4,
}

/// <summary>Kredi hareketinin gerekçesi (kim/neden için izlenebilirlik).</summary>
public enum MakeupCreditReason
{
    /// <summary>Stüdyo dersi iptal etti.</summary>
    StudioCancelledClass = 0,

    /// <summary>Tatil/toplu kapanış.</summary>
    HolidayClosure = 1,

    /// <summary>Owner/StudioAdmin manuel verdi.</summary>
    ManualByOwner = 2,

    /// <summary>Instructor manuel verdi.</summary>
    ManualByInstructor = 3,

    /// <summary>Telafi rezervasyonunda kullanıldı.</summary>
    MakeupBookingUsed = 4,

    /// <summary>Telafi rezervasyonu iptal edildi → geri verildi.</summary>
    MakeupBookingCancelled = 5,

    /// <summary>Öğrenci zamanında iptal etti (bu sürümde otomatik tetiklenmez).</summary>
    StudentCancelledInTime = 6,

    /// <summary>Geç iptal yoklamasından manuel onaylı kredi (Sprint 6 — LateCancel).</summary>
    NoShowApproved = 7,

    Other = 8,

    /// <summary>Mazeretli devamsızlık yoklamasından manuel verilen kredi (Sprint 6 — Excused).</summary>
    ExcusedAbsence = 9,

    /// <summary>Devamsızlık (Absent) yoklamasından otomatik kazanılan telafi (Sprint 19 — sözleşme MakeupAllowance limitli).</summary>
    AbsentEarned = 10,

    /// <summary>Ders hoca nedeniyle işlenemedi (Sprint 20) → öğrencilere ekstra telafi (MakeupAllowance'tan bağımsız).</summary>
    InstructorNotDelivered = 11,
}

/// <summary>
/// Telafi dersi talebinin durumu (Sprint 27 — Instructor Makeup Request). Pending talep gerçek ders DEĞİLDİR;
/// programda "Onay bekliyor" görünür, hoca+öğrenci saatini bloklar, krediyi REZERVE eder (ledger'a yazılmaz).
/// Approve → gerçek Makeup ClassSession+ClassBooking materyalize edilir, kredi Use(−1) ile gerçekten tüketilir.
/// Rejected/Withdrawn kayıtlar AUDIT için saklanır (soft-delete edilmez) ve çakışmaya DAHİL EDİLMEZ.
/// </summary>
public enum MakeupRequestStatus
{
    /// <summary>Onay bekliyor (aktif): saati bloklar + krediyi rezerve eder.</summary>
    Pending = 0,

    /// <summary>Owner/StudioAdmin onayladı → gerçek telafi dersine dönüştü (kredi tüketildi).</summary>
    Approved = 1,

    /// <summary>Owner/StudioAdmin reddetti → slot boşaldı, rezerve serbest (ledger değişmez).</summary>
    Rejected = 2,

    /// <summary>Talebi açan (veya manager) geri çekti → slot boşaldı, rezerve serbest.</summary>
    Withdrawn = 3,
}

/// <summary>Toplu kapanış (StudioClosure) işleminin etkilenen seanslara uygulayacağı aksiyon.</summary>
public enum StudioClosureActionType
{
    /// <summary>Aralıktaki planlı seansları iptal eder.</summary>
    CancelSessions = 0,

    /// <summary>Seansları iptal eder ve aktif rezervasyon sahiplerine telafi kredisi verir.</summary>
    CancelAndGrantMakeupCredit = 1,

    /// <summary>Seansları gün ofseti kadar ileri alır (çakışan/uygunsuz olanlar atlanır).</summary>
    RescheduleByOffset = 2,
}
