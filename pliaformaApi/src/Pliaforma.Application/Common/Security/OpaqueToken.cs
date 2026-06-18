using System.Security.Cryptography;
using System.Text;

namespace Pliaforma.Application.Common.Security;

/// <summary>
/// Tek-kullanımlık opak token (Sprint 16: e-posta doğrulama / şifre sıfırlama). Ham token CSPRNG ile
/// URL-güvenli üretilir; DB'de yalnız <see cref="Hash"/> (SHA-256, Base64) saklanır. Ham değer sadece
/// kullanıcıya giden linke konur.
/// </summary>
public static class OpaqueToken
{
    /// <summary>32 baytlık CSPRNG → URL-güvenli Base64 (≈43 karakter).</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Ham token'ın DB'de aranacak deterministik hash'i (SHA-256, Base64).</summary>
    public static string Hash(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(hash);
    }
}
