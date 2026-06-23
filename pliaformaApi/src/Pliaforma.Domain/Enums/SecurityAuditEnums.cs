namespace Pliaforma.Domain.Enums;

/// <summary>
/// Güvenlik denetim olay türleri (SecurityAuditLog). Sprint 31B-2 kapsamı yalnız kimlik/oturum
/// olaylarıdır; rol/ödeme/sözleşme/export/HealthNotes erişimi 31C'ye bırakılmıştır.
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
}

/// <summary>Denetim olayının sonucu.</summary>
public enum SecurityAuditOutcome
{
    Success = 0,
    Failure = 1,
}
