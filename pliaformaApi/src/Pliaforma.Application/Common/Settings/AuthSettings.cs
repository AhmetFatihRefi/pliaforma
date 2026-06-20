namespace Pliaforma.Application.Common.Settings;

/// <summary>
/// Kimlik/doğrulama davranış ayarları (config "Auth" bölümü). Prod-GÜVENLİ varsayılan: e-posta doğrulama ZORUNLU.
/// Dev'de kapatmak için appsettings.Development.json → "Auth": { "RequireEmailVerification": false }.
/// </summary>
public sealed class AuthSettings
{
    public const string SectionName = "Auth";

    /// <summary>
    /// E-posta doğrulama zorunlu mu?
    /// <para>true (varsayılan / PROD): davet/başvuru kabulü doğrulanmış e-posta ister; yeni kayıt
    /// <c>EmailVerifiedAt=null</c> kalır + doğrulama e-postası gönderilir.</para>
    /// <para>false (DEV): kabul kapısı doğrulama BEKLEMEZ; yeni kayıtlar otomatik doğrulanmış sayılır
    /// (<c>EmailVerifiedAt</c> set edilir, doğrulama e-postası gönderilmez).</para>
    /// </summary>
    public bool RequireEmailVerification { get; set; } = true;
}
