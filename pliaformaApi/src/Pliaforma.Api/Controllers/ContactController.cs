using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Pliaforma.Api.RateLimiting;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Models;
using Pliaforma.Infrastructure.Settings;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Sprint 52 — public iletişim/demo talep formu. DB'ye YAZMAZ; içerik yalnız Email:ContactToAddress'e
/// (operatörün private adresi, deploy/.env CONTACT_TO_EMAIL) e-postalanır. AllowAnonymous + IP rate-limit
/// (saatte 5) + body size limit + honeypot. Hata yanıtları teknik detay/SMTP bilgisi SIZDIRMAZ.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/contact")]
[EnableRateLimiting(RateLimitPolicies.Contact)]
public sealed class ContactController : ControllerBase
{
    private const int MaxBytes = 16 * 1024; // form alanı toplam üst sınırlarının çok üstü; oversize → 413

    private readonly IEmailSender _emailSender;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<ContactController> _logger;

    public ContactController(IEmailSender emailSender, IOptions<EmailSettings> emailSettings, ILogger<ContactController> logger)
    {
        _emailSender = emailSender;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Form gövdesi. Tüm alanlar string — binding hatası üretmez; doğrulama elle yapılır ki yanıt
    /// mevcut ApiResponse zarfıyla dönsün. <see cref="Website"/> honeypot'tur: insan görmez, bot doldurur.
    /// </summary>
    public sealed record ContactRequest(
        string? FullName,
        string? StudioName,
        string? Email,
        string? Phone,
        string? City,
        string? Message,
        string? Website);

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Send([FromBody] ContactRequest request, CancellationToken ct)
    {
        // Honeypot: dolu geldiyse bot — sahte başarı dön (botu bilgilendirme), hiçbir şey gönderme.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            _logger.LogInformation("Contact honeypot tetiklendi; gönderim yapılmadı.");
            return Ok(ApiResponse<object?>.Ok(null, "Mesajınız alındı."));
        }

        var (message, errors) = Validate(request);
        if (message is null)
            return BadRequest(ApiResponse<object?>.Fail("Formda eksik veya hatalı alanlar var.", errors));

        // Alıcı yapılandırılmamışsa özellik kapalıdır — teknik detay vermeden 503.
        if (string.IsNullOrWhiteSpace(_emailSettings.ContactToAddress))
        {
            _logger.LogWarning("Contact formu çağrıldı ama Email:ContactToAddress yapılandırılmamış (CONTACT_TO_EMAIL).");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object?>.Fail("Mesaj gönderimi şu anda kullanılamıyor. Lütfen daha sonra tekrar deneyin."));
        }

        try
        {
            await _emailSender.SendContactMessageAsync(message, ct);
        }
        catch (Exception ex)
        {
            // SMTP/host detayı yanıtta YOK; yalnız log. İçerik (PII) loglanmaz.
            _logger.LogError(ex, "Contact formu e-postası gönderilemedi.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object?>.Fail("Mesajınız gönderilemedi. Lütfen daha sonra tekrar deneyin."));
        }

        return Ok(ApiResponse<object?>.Ok(null, "Mesajınız alındı. En kısa sürede size dönüş yapacağız."));
    }

    /// <summary>
    /// Elle doğrulama: zorunlular (ad soyad, stüdyo, mesaj) + e-posta/telefondan en az biri + üst uzunluk
    /// sınırları. Tek satırlık alanlardan CR/LF temizlenir. Başarıda normalize edilmiş ContactMessage döner.
    /// </summary>
    private static (ContactMessage? Message, IReadOnlyDictionary<string, string[]>? Errors) Validate(ContactRequest r)
    {
        static string? Line(string? v)
        {
            var s = v?.Replace("\r", " ").Replace("\n", " ").Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        var fullName = Line(r.FullName);
        var studioName = Line(r.StudioName);
        var email = Line(r.Email);
        var phone = Line(r.Phone);
        var city = Line(r.City);
        var body = r.Message?.Trim();

        var errors = new Dictionary<string, string[]>();

        if (fullName is null || fullName.Length is < 2 or > 100)
            errors["fullName"] = ["Ad soyad 2-100 karakter olmalı."];
        if (studioName is null || studioName.Length is < 2 or > 150)
            errors["studioName"] = ["Stüdyo adı 2-150 karakter olmalı."];
        if (string.IsNullOrWhiteSpace(body) || body.Length is < 10 or > 2000)
            errors["message"] = ["Mesaj 10-2000 karakter olmalı."];
        if (email is null && phone is null)
            errors["email"] = ["E-posta veya telefondan en az biri gerekli."];
        if (email is { Length: > 200 } || (email is not null && !System.Net.Mail.MailAddress.TryCreate(email, out _)))
            errors["email"] = ["Geçerli bir e-posta adresi girin."];
        if (phone is { Length: > 40 })
            errors["phone"] = ["Telefon en fazla 40 karakter olmalı."];
        if (city is { Length: > 100 })
            errors["city"] = ["Şehir en fazla 100 karakter olmalı."];

        if (errors.Count > 0)
            return (null, errors);

        return (new ContactMessage(fullName!, studioName!, email, phone, city, body!), null);
    }
}
