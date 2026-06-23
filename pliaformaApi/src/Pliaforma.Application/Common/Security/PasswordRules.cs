using System.Text.RegularExpressions;
using FluentValidation;

namespace Pliaforma.Application.Common.Security;

/// <summary>
/// Ortak parola politikası (Sprint 31B-2): min 10, en az birer büyük/küçük harf + rakam + özel karakter,
/// max 128, yaygın parola reddi. Register / RegisterStudio / PasswordReset / ChangePassword AYNI kuraldan
/// beslenir (<c>RuleFor(x =&gt; x.Password).Password();</c>).
/// <para>Mevcut kullanıcılar <b>login sırasında</b> bu kurala TABİ DEĞİLDİR — politika yalnız yeni parola
/// belirleme akışlarında uygulanır.</para>
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 10;
    public const int MaxLength = 128;

    private static readonly Regex Upper = new("[A-Z]", RegexOptions.Compiled);
    private static readonly Regex Lower = new("[a-z]", RegexOptions.Compiled);
    private static readonly Regex Digit = new("[0-9]", RegexOptions.Compiled);
    private static readonly Regex Special = new("[^A-Za-z0-9]", RegexOptions.Compiled);

    private static readonly Lazy<HashSet<string>> CommonPasswords = new(Load);

    /// <summary>Parola yaygın/zayıf listede mi? (case-insensitive, trim'li).</summary>
    public static bool IsCommon(string? password) =>
        !string.IsNullOrWhiteSpace(password) && CommonPasswords.Value.Contains(password.Trim());

    /// <summary>FluentValidation kuralı. Tüm parola-belirleme akışlarında aynı şekilde kullanılır.</summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Parola gerekli.")
            .MinimumLength(MinLength).WithMessage($"Parola en az {MinLength} karakter olmalı.")
            .MaximumLength(MaxLength).WithMessage($"Parola en fazla {MaxLength} karakter olabilir.")
            .Must(p => p is not null && Upper.IsMatch(p)).WithMessage("Parola en az bir büyük harf içermeli.")
            .Must(p => p is not null && Lower.IsMatch(p)).WithMessage("Parola en az bir küçük harf içermeli.")
            .Must(p => p is not null && Digit.IsMatch(p)).WithMessage("Parola en az bir rakam içermeli.")
            .Must(p => p is not null && Special.IsMatch(p)).WithMessage("Parola en az bir özel karakter içermeli.")
            .Must(p => !IsCommon(p)).WithMessage("Bu parola çok yaygın; lütfen daha güçlü bir parola seçin.");

    private static HashSet<string> Load()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var asm = typeof(PasswordRules).Assembly;
        var name = Array.Find(asm.GetManifestResourceNames(),
            n => n.EndsWith("common-passwords.txt", StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return set;

        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null)
            return set;

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                set.Add(trimmed);
        }
        return set;
    }
}
