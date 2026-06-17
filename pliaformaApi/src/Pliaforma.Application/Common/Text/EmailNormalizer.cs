namespace Pliaforma.Application.Common.Text;

/// <summary>E-posta benzersizlik karşılaştırmaları için tek tip normalizasyon.</summary>
public static class EmailNormalizer
{
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
