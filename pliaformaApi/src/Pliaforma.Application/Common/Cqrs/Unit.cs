namespace Pliaforma.Application.Common.Cqrs;

/// <summary>Değer döndürmeyen komutlar için boş yanıt tipi.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
