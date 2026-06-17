using Pliaforma.Application.Common.Interfaces;

namespace Pliaforma.Infrastructure.Services;

/// <summary>BCrypt tabanlı parola hash'leme. Algoritma detayı bu katmanda kalır.</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
