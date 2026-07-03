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

    /// <summary>
    /// Sprint 52: public iletişim/demo formu içeriğini operatörün private adresine (Email:ContactToAddress)
    /// iletir. DB'ye YAZILMAZ; yalnız e-posta. Alıcı adres yapılandırılmamışsa implementasyon fırlatır —
    /// çağıran (ContactController) göndermeden önce yapılandırmayı kontrol eder.
    /// </summary>
    Task SendContactMessageAsync(ContactMessage message, CancellationToken ct = default);
}

/// <summary>
/// İletişim/demo talebi içeriği (Sprint 52). Alanlar kullanıcı girdisidir — HTML gövdeye yazan taraf
/// encode etmekle yükümlüdür. Email/Phone'dan en az biri dolu gelir (controller doğrular).
/// </summary>
public sealed record ContactMessage(
    string FullName,
    string StudioName,
    string? Email,
    string? Phone,
    string? City,
    string Message);
