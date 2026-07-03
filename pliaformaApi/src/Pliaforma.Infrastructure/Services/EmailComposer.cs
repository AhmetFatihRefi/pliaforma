using System.Text.Encodings.Web;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Infrastructure.Settings;

namespace Pliaforma.Infrastructure.Services;

/// <summary>Doğrulama/sıfırlama e-postalarının link + içerik kompozisyonu (Sprint 16). Link tabanı PublicOrigin.</summary>
internal static class EmailComposer
{
    public sealed record Composed(string Subject, string HtmlBody, string TextBody, string Link);

    public static Composed Verification(EmailSettings s, string fullName, string rawToken)
    {
        var link = $"{s.PublicOrigin.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(rawToken)}";
        var subject = "Pliaforma — E-posta adresinizi doğrulayın";
        // Düz metin gövdede HTML anlamı yoktur; kullanıcı adı encode EDİLMEZ.
        var text =
            $"Merhaba {fullName},\n\nPliaforma hesabınızı etkinleştirmek için e-posta adresinizi doğrulayın:\n{link}\n\n" +
            "Bu bağlantı 48 saat geçerlidir. Bu işlemi siz başlatmadıysanız bu e-postayı yok sayabilirsiniz.";
        // HTML gövdede kullanıcı kaynaklı fullName ve link encode edilir (HTML-injection önlemi).
        var nameHtml = HtmlEncoder.Default.Encode(fullName);
        var linkAttr = HtmlEncoder.Default.Encode(link);
        var html =
            $"<p>Merhaba {nameHtml},</p><p>Pliaforma hesabınızı etkinleştirmek için e-posta adresinizi doğrulayın:</p>" +
            $"<p><a href=\"{linkAttr}\">E-postamı doğrula</a></p><p>Bu bağlantı 48 saat geçerlidir.</p>";
        return new Composed(subject, html, text, link);
    }

    public static Composed PasswordReset(EmailSettings s, string fullName, string rawToken)
    {
        var link = $"{s.PublicOrigin.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var subject = "Pliaforma — Şifre sıfırlama";
        // Düz metin gövdede HTML anlamı yoktur; kullanıcı adı encode EDİLMEZ.
        var text =
            $"Merhaba {fullName},\n\nŞifrenizi sıfırlamak için aşağıdaki bağlantıyı kullanın:\n{link}\n\n" +
            "Bu bağlantı 60 dakika geçerlidir. Bu işlemi siz başlatmadıysanız bu e-postayı yok sayabilirsiniz.";
        // HTML gövdede kullanıcı kaynaklı fullName ve link encode edilir (HTML-injection önlemi).
        var nameHtml = HtmlEncoder.Default.Encode(fullName);
        var linkAttr = HtmlEncoder.Default.Encode(link);
        var html =
            $"<p>Merhaba {nameHtml},</p><p>Şifrenizi sıfırlamak için aşağıdaki bağlantıyı kullanın:</p>" +
            $"<p><a href=\"{linkAttr}\">Şifremi sıfırla</a></p><p>Bu bağlantı 60 dakika geçerlidir.</p>";
        return new Composed(subject, html, text, link);
    }

    /// <summary>
    /// Sprint 52: iletişim/demo formu → operatöre iç bildirim. TÜM alanlar kullanıcı girdisidir ve HTML
    /// gövdede encode edilir (HTML-injection önlemi). Subject kullanıcı girdisi İÇERMEZ (header-injection yok).
    /// </summary>
    public static Composed Contact(ContactMessage m)
    {
        const string subject = "Pliaforma — Yeni iletişim / demo talebi";

        static string E(string? v) => HtmlEncoder.Default.Encode(v ?? "-");
        static string T(string? v) => string.IsNullOrWhiteSpace(v) ? "-" : v;

        var text =
            "Pliaforma web sitesi iletişim formundan yeni bir mesaj geldi.\n\n" +
            $"Ad Soyad : {T(m.FullName)}\n" +
            $"Stüdyo   : {T(m.StudioName)}\n" +
            $"E-posta  : {T(m.Email)}\n" +
            $"Telefon  : {T(m.Phone)}\n" +
            $"Şehir    : {T(m.City)}\n\n" +
            $"Mesaj:\n{T(m.Message)}\n";

        var html =
            "<p><strong>Pliaforma web sitesi iletişim formundan yeni bir mesaj geldi.</strong></p>" +
            "<table cellpadding=\"4\">" +
            $"<tr><td><strong>Ad Soyad</strong></td><td>{E(m.FullName)}</td></tr>" +
            $"<tr><td><strong>Stüdyo</strong></td><td>{E(m.StudioName)}</td></tr>" +
            $"<tr><td><strong>E-posta</strong></td><td>{E(m.Email)}</td></tr>" +
            $"<tr><td><strong>Telefon</strong></td><td>{E(m.Phone)}</td></tr>" +
            $"<tr><td><strong>Şehir</strong></td><td>{E(m.City)}</td></tr>" +
            "</table>" +
            $"<p><strong>Mesaj:</strong></p><p>{E(m.Message).Replace("\n", "<br/>")}</p>";

        return new Composed(subject, html, text, Link: string.Empty);
    }
}
