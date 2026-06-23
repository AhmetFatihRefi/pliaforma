namespace Pliaforma.Application.Common.Interfaces;

/// <summary>RFC 6238 TOTP üretimi/doğrulaması (harici bağımlılık yok).</summary>
public interface ITotpService
{
    /// <summary>Yeni base32 TOTP secret üretir.</summary>
    string GenerateSecret();

    /// <summary>Authenticator app'lere verilecek otpauth:// URI'si (QR frontend'de render edilir).</summary>
    string BuildOtpAuthUri(string secretBase32, string accountName, string issuer);

    /// <summary>
    /// 6 haneli kodu doğrular (±1 adım tolerans). Replay koruması: kabul edilen <paramref name="matchedTimestep"/>,
    /// <paramref name="lastUsedTimestep"/>'ten büyük olmalı (aynı timestep iki kez kullanılamaz).
    /// </summary>
    bool VerifyCode(string secretBase32, string code, long lastUsedTimestep, out long matchedTimestep);
}

/// <summary>TOTP secret'larını AES-256-GCM ile şifreler/çözer (Mfa:EncryptionKey).</summary>
public interface IMfaSecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

/// <summary>MFA challenge için stateless kısa-ömürlü imzalı token (purpose=mfa_challenge, ayrı audience).</summary>
public interface IMfaChallengeService
{
    string CreateChallengeToken(Guid userId);

    /// <summary>Geçerliyse userId döner; değilse null (süre dolmuş/imza/aud/purpose hatası).</summary>
    Guid? ValidateChallengeToken(string token);
}
