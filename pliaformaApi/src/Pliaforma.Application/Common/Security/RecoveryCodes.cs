using System.Security.Cryptography;
using System.Text;

namespace Pliaforma.Application.Common.Security;

/// <summary>
/// MFA recovery code üretimi/hash'i (Sprint 31-MFA). Plain kod SAKLANMAZ — yalnız SHA-256 hash. Karışabilecek
/// karakterler (0/O, 1/I) alfabe dışında. Biçim: <c>XXXXX-XXXXX</c>.
/// </summary>
public static class RecoveryCodes
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var chars = new char[10];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars, 0, 5) + "-" + new string(chars, 5, 5);
    }

    /// <summary>Karşılaştırma için normalize: yalnız harf/rakam, büyük harf.</summary>
    public static string Normalize(string? code) =>
        new string((code ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)));
        return Convert.ToBase64String(bytes);
    }
}
