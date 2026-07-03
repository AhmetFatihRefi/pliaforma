namespace Pliaforma.Domain.Enums;

/// <summary>
/// Sprint 53 — refresh token'ın verildiği istemci kanalı (kanal bağlama / channel binding).
/// Web token'ı yalnız web (cookie) uçlarında, Mobile token'ı yalnız <c>api/auth/mobile</c> uçlarında
/// geçerlidir; uyumsuz kanal generic 401'dir. Kolonda string saklanır (bkz. RefreshTokenConfiguration).
/// </summary>
public enum AuthClient
{
    /// <summary>Tarayıcı: refresh token httpOnly cookie ile taşınır (mevcut model, değişmedi).</summary>
    Web = 0,

    /// <summary>Native mobil uygulama: refresh token response body ile döner, OS güvenli deposunda saklanır.</summary>
    Mobile = 1,
}
