using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Settings;

namespace Pliaforma.Infrastructure.Services;

/// <summary>
/// TOTP secret'larını AES-256-GCM ile şifreler. Çıktı biçimi: base64( nonce(12) | tag(16) | ciphertext ).
/// Anahtar <c>Mfa:EncryptionKey</c> (base64, ≥32 bayt). Yalnız MFA akışlarında resolve edilir (Mfa:Enabled=false iken kullanılmaz).
/// </summary>
public sealed class MfaSecretProtector : IMfaSecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public MfaSecretProtector(IOptions<MfaSettings> mfa)
    {
        var raw = Convert.FromBase64String(mfa.Value.EncryptionKey);
        if (raw.Length < 32)
            throw new InvalidOperationException("Mfa:EncryptionKey en az 32 bayt (base64) olmalı.");
        _key = raw.Length == 32 ? raw : raw[..32]; // AES-256 için tam 32 bayt
    }

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var combined = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, combined, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(combined);
    }

    public string Unprotect(string protectedValue)
    {
        var combined = Convert.FromBase64String(protectedValue);
        var nonce = combined[..NonceSize];
        var tag = combined[NonceSize..(NonceSize + TagSize)];
        var cipher = combined[(NonceSize + TagSize)..];
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
