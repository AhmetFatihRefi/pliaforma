using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Infrastructure.Settings;

namespace Pliaforma.Infrastructure.Services;

/// <summary>
/// Üretim e-posta gönderici (Sprint 16): SMTP env değerleriyle gerçek gönderim. DI fail-fast, SenderMode=Smtp
/// iken Host/From/PublicOrigin zorunluluğunu garanti eder. Dev/test bu modu kullanmaz (Log).
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;

    public SmtpEmailSender(IOptions<EmailSettings> settings) => _settings = settings.Value;

    public Task SendEmailVerificationAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default)
        => SendAsync(toEmail, EmailComposer.Verification(_settings, fullName, rawToken), ct);

    public Task SendPasswordResetAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default)
        => SendAsync(toEmail, EmailComposer.PasswordReset(_settings, fullName, rawToken), ct);

    /// <summary>
    /// Sprint 52: iletişim formu → ContactToAddress (operatörün private adresi). From mevcut noreply
    /// kimliğidir; kullanıcının e-postası YALNIZ Reply-To olur ve MailAddress.TryCreate ile doğrulanır
    /// (geçersizse Reply-To hiç eklenmez — header injection / bozuk adres riski yok).
    /// </summary>
    public Task SendContactMessageAsync(ContactMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ContactToAddress))
            throw new InvalidOperationException(
                "Email:ContactToAddress yapılandırılmamış (deploy/.env CONTACT_TO_EMAIL). İletişim formu gönderilemez.");

        MailAddress? replyTo = null;
        if (!string.IsNullOrWhiteSpace(message.Email))
            MailAddress.TryCreate(message.Email.Trim(), out replyTo);

        return SendAsync(_settings.ContactToAddress, EmailComposer.Contact(message), ct, replyTo);
    }

    private async Task SendAsync(string toEmail, EmailComposer.Composed msg, CancellationToken ct, MailAddress? replyTo = null)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromName),
            Subject = msg.Subject,
            Body = msg.HtmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);
        if (replyTo is not null)
            message.ReplyToList.Add(replyTo);

        using var client = new SmtpClient(_settings.Smtp.Host, _settings.Smtp.Port)
        {
            EnableSsl = _settings.Smtp.UseTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
        if (!string.IsNullOrWhiteSpace(_settings.Smtp.Username))
            client.Credentials = new NetworkCredential(_settings.Smtp.Username, _settings.Smtp.Password);

        await client.SendMailAsync(message, ct);
    }
}
