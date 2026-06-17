using System.Security.Cryptography;

namespace Pliaforma.Application.Common.Text;

/// <summary>
/// Stüdyo katılım kodu (Sprint 15): 10 karakter, Base32-benzeri, karışan karakterler (0/O, 1/I/L) yok,
/// CSPRNG ile üretilir. <see cref="Normalize"/> tireleri/boşlukları atıp büyük harfe çevirir → kullanıcı
/// "ABCD-EFGH-JK" veya "abcdefghjk" girse de aynı koda eşlenir. Global benzersizlik DB tarafında (üretirken
/// çakışma kontrol döngüsü) sağlanır.
/// </summary>
public static class JoinCode
{
    // 31 karakter: 0,1,O,I,L çıkarıldı (okunabilirlik). Crockford Base32'ye yakın.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    public const int Length = 10;

    /// <summary>Tahmin edilemez yeni bir kod üretir (benzersizlik kontrolü çağırana aittir).</summary>
    public static string Generate()
    {
        Span<char> buffer = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(buffer);
    }

    /// <summary>Girdiyi normalleştirir: büyük harf + yalnız alfabe karakterleri (tire/boşluk atılır).</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var upper = input.Trim().ToUpperInvariant();
        Span<char> buffer = stackalloc char[upper.Length];
        var n = 0;
        foreach (var ch in upper)
            if (Alphabet.IndexOf(ch) >= 0)
                buffer[n++] = ch;
        return new string(buffer[..n]);
    }

    /// <summary>Görüntü için gruplu biçim: ABCD-EFGH-JK.</summary>
    public static string Format(string code)
    {
        if (string.IsNullOrEmpty(code)) return code;
        var parts = new List<string>();
        for (var i = 0; i < code.Length; i += 4)
            parts.Add(code.Substring(i, Math.Min(4, code.Length - i)));
        return string.Join('-', parts);
    }
}
