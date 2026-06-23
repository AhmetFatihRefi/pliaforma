using System.Security.Cryptography;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;

namespace Pliaforma.Infrastructure.Services;

/// <summary>RFC 6238 TOTP (HMAC-SHA1, 6 hane, 30 sn adım, ±1 tolerans). Harici bağımlılık yok.</summary>
public sealed class TotpService : ITotpService
{
    private const int Digits = 6;
    private const int PeriodSeconds = 30;

    public string GenerateSecret() =>
        Base32.Encode(RandomNumberGenerator.GetBytes(20)); // 160 bit (RFC 6238 önerisi)

    public string BuildOtpAuthUri(string secretBase32, string accountName, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var iss = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secretBase32}&issuer={iss}&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
    }

    public bool VerifyCode(string secretBase32, string code, long lastUsedTimestep, out long matchedTimestep)
    {
        matchedTimestep = 0;
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits || !code.All(char.IsDigit))
            return false;

        var key = Base32.Decode(secretBase32);
        if (key.Length == 0)
            return false;

        long currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodSeconds;
        for (long step = currentStep - 1; step <= currentStep + 1; step++)
        {
            if (step <= lastUsedTimestep)
                continue; // replay koruması: bu adım daha önce kullanıldı
            if (CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(Compute(key, step)),
                    System.Text.Encoding.ASCII.GetBytes(code)))
            {
                matchedTimestep = step;
                return true;
            }
        }
        return false;
    }

    private static string Compute(byte[] key, long step)
    {
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counter);

        var hash = HMACSHA1.HashData(key, counter);
        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];
        int otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString().PadLeft(Digits, '0');
    }
}
