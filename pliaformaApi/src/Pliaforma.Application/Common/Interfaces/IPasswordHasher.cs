namespace Pliaforma.Application.Common.Interfaces;

/// <summary>Parola hash'leme soyutlaması. Somut algoritma (BCrypt) Infrastructure'dadır.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
