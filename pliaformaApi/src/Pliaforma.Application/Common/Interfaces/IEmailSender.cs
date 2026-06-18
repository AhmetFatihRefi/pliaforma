namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// E-posta gönderim soyutlaması (Sprint 16). Link tabanı (PublicOrigin) + içerik kompozisyonu **Infrastructure
/// implementasyonundadır** — Application yalnız ham token + alıcı bilgisini verir. Mod (Log/Smtp/Disabled)
/// ve SMTP ayarları Infrastructure'da; Application sağlayıcıya bağlanmaz (IPasswordHasher deseni).
/// </summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default);

    Task SendPasswordResetAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default);
}
