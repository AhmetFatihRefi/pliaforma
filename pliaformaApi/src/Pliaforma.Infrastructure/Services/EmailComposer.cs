using System.Text.Encodings.Web;
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
}
