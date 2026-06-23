namespace Pliaforma.Application.Common.Security;

/// <summary>
/// PII maskeleme yardımcıları (denetim/log için). Ham değer SAKLANMAZ. KVKK veri minimizasyonu.
/// </summary>
public static class PiiMask
{
    /// <summary>
    /// E-postayı maskeler: <c>"john.doe@example.com"</c> → <c>"j***@e***.com"</c>.
    /// Boş/geçersiz girdide ya da '@' yoksa kısmi/tam maske döner; saklamaya değer ham bilgi bırakmaz.
    /// </summary>
    public static string? Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        email = email.Trim();
        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
            return "***";

        var local = email[..at];
        var domain = email[(at + 1)..];

        var localMasked = local[0] + "***";

        var dot = domain.LastIndexOf('.');
        var domainMasked = dot > 0
            ? domain[0] + "***" + domain[dot..]   // "e***.com"
            : domain[0] + "***";

        return localMasked + "@" + domainMasked;
    }
}
