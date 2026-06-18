namespace Pliaforma.Domain.Enums;

/// <summary>Tek-kullanımlık auth token'ın amacı (Sprint 16).</summary>
public enum AuthTokenPurpose
{
    /// <summary>E-posta doğrulama bağlantısı.</summary>
    EmailVerification = 0,

    /// <summary>Şifre sıfırlama bağlantısı.</summary>
    PasswordReset = 1,
}
