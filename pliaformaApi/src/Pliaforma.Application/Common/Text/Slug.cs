using System.Globalization;
using System.Text;

namespace Pliaforma.Application.Common.Text;

/// <summary>URL-dostu slug üretimi (Türkçe karakter dönüşümü dahil).</summary>
public static class Slug
{
    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        ['ç'] = 'c', ['ğ'] = 'g', ['ı'] = 'i', ['ö'] = 'o', ['ş'] = 's', ['ü'] = 'u',
    };

    public static string From(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "studio";

        var lower = input.Trim().ToLower(new CultureInfo("tr-TR"));
        var sb = new StringBuilder(lower.Length);
        var lastDash = false;

        foreach (var ch in lower)
        {
            var c = TurkishMap.TryGetValue(ch, out var mapped) ? mapped : ch;

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(c);
                lastDash = false;
            }
            else if (!lastDash && sb.Length > 0)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "studio" : slug;
    }
}
