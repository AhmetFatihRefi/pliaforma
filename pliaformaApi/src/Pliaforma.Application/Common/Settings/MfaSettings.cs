namespace Pliaforma.Application.Common.Settings;

/// <summary>
/// MFA (TOTP) ayarları (config "Mfa" bölümü). <see cref="Enabled"/> master switch'tir:
/// PROD'da true olmalı (kapalıysa fail-fast), Development/test'te false (mevcut smoke/E2E yeşil kalsın).
/// </summary>
public sealed class MfaSettings
{
    public const string SectionName = "Mfa";

    /// <summary>MFA zorunluluğu açık mı? Prod=true, dev/test=false.</summary>
    public bool Enabled { get; set; }

    /// <summary>TOTP secret'larını şifrelemek için AES-256-GCM anahtarı (base64, ≥32 bayt). Prod'da env zorunlu.</summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>otpauth:// etiketi için issuer adı.</summary>
    public string Issuer { get; set; } = "Pliaforma";

    /// <summary>MFA challenge token ömrü (dakika).</summary>
    public int ChallengeTokenMinutes { get; set; } = 5;

    /// <summary>Setup sonrası üretilecek tek-kullanımlık recovery code sayısı.</summary>
    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>
    /// Sprint 50 — "Bu cihazı hatırla" güven süresi (gün). Kullanıcı MFA doğrularken "bu cihazda tekrar sorma"
    /// işaretlerse, aynı cihazda bu kadar gün MFA atlanır (fixed window). Varsayılan 30. Prod'da
    /// <c>Mfa__TrustedDeviceDays</c> ile değiştirilebilir. MFA zorunluluğunu KALDIRMAZ.
    /// </summary>
    public int TrustedDeviceDays { get; set; } = 30;
}
