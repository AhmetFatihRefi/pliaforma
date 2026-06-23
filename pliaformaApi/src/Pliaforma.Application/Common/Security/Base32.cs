using System.Text;

namespace Pliaforma.Application.Common.Security;

/// <summary>RFC 4648 Base32 (TOTP secret kodlama/çözme). Pad'siz, büyük harf. Harici bağımlılık yok.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return string.Empty;

        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = data[0], bitsLeft = 8, next = 1;
        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer = (buffer << 8) | data[next++];
                    bitsLeft += 8;
                }
                else
                {
                    buffer <<= (5 - bitsLeft);
                    bitsLeft = 5;
                }
            }
            int index = (buffer >> (bitsLeft - 5)) & 0x1F;
            bitsLeft -= 5;
            sb.Append(Alphabet[index]);
        }
        return sb.ToString();
    }

    public static byte[] Decode(string base32)
    {
        if (string.IsNullOrEmpty(base32))
            return Array.Empty<byte>();

        base32 = base32.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>(base32.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in base32)
        {
            int val = Alphabet.IndexOf(c);
            if (val < 0)
                continue; // boşluk vb. atla
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }
        return output.ToArray();
    }
}
