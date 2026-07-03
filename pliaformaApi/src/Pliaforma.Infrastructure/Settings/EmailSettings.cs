namespace Pliaforma.Infrastructure.Settings;

/// <summary>E-posta gönderim modu (Sprint 16). Dev/test=Log, prod=Smtp, Disabled=bilinçli no-op.</summary>
public enum EmailSenderMode
{
    /// <summary>Gerçek gönderim yok; link/içerik loglanır (dev/test).</summary>
    Log = 0,

    /// <summary>SMTP ile gerçek gönderim (prod).</summary>
    Smtp = 1,

    /// <summary>No-op (yalnız bilinçli kullanım; prod'da override gerektirir).</summary>
    Disabled = 2,
}

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public EmailSenderMode SenderMode { get; set; } = EmailSenderMode.Log;

    public string FromAddress { get; set; } = "noreply@pliaforma.local";
    public string FromName { get; set; } = "Pliaforma";

    /// <summary>Kullanıcıya görünen (frontend) origin — doğrulama/sıfırlama linkleri bununla kurulur.</summary>
    public string PublicOrigin { get; set; } = "http://localhost:3000";

    /// <summary>Prod'da Disabled modunu BİLİNÇLİ açmak için (aksi halde fail-fast).</summary>
    public bool AllowDisabledInProduction { get; set; }

    /// <summary>
    /// Sprint 52: public iletişim/demo formunun iletileceği PRIVATE alıcı (Email__ContactToAddress /
    /// deploy/.env CONTACT_TO_EMAIL). Frontend'e ASLA gönderilmez. Boşsa contact endpoint'i 503 döner
    /// (fail-fast YOK — form opsiyonel bir özelliktir, uygulama açılışını engellemez).
    /// </summary>
    public string ContactToAddress { get; set; } = string.Empty;

    public SmtpSettings Smtp { get; set; } = new();
}

public sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = true;
}
