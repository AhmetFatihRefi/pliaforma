namespace Pliaforma.Domain.Enums;

/// <summary>
/// Güvenlik denetim olay türleri (SecurityAuditLog). 31B-2: kimlik/oturum; 31-MFA: MFA; 31C-1: rol/üyelik,
/// ödeme, sözleşme, öğrenci verisi/HealthNotes erişimi. Değerler kolonda <b>string</b> saklanır
/// (HasConversion&lt;string&gt;) → yeni değer eklemek <b>migration GEREKTİRMEZ</b> (ad ≤40 char olmalı).
/// </summary>
public enum SecurityAuditEventType
{
    LoginSucceeded = 0,
    LoginFailed = 1,
    AccountLockedOut = 2,
    Logout = 3,
    PasswordResetRequested = 4,
    PasswordResetCompleted = 5,
    PasswordChanged = 6,

    // Sprint 31-MFA
    MFASetupStarted = 7,
    MFASetupCompleted = 8,
    MFAChallengeCreated = 9,
    MFASucceeded = 10,
    MFAFailed = 11,
    MFARecoveryCodeUsed = 12,
    MFAResetCompleted = 13,

    // Sprint 31C-1 — rol/üyelik
    RoleChanged = 14,
    MemberInvited = 15,
    MembershipAccepted = 16,
    MembershipRejected = 17,
    MemberRemoved = 18,

    // Sprint 31C-1 — ödeme
    PaymentCreated = 19,
    PaymentUpdated = 20,
    PaymentMarkedPaid = 21,
    PaymentCancelled = 22,

    // Sprint 31C-1 — sözleşme
    ContractCreated = 23,
    ContractAmended = 24,
    ContractCancelled = 25,

    // Sprint 31C-1 — öğrenci verisi / hassas alan erişimi (istek-başı tek event)
    StudentDataListed = 26,
    HealthNotesAccessed = 27,

    // Sprint 31D-1 — retention purge meta olayı (tur başına ≤1; PII yok)
    AuditRetentionPurge = 28,
}

/// <summary>Denetim olayının sonucu.</summary>
public enum SecurityAuditOutcome
{
    Success = 0,
    Failure = 1,
}
