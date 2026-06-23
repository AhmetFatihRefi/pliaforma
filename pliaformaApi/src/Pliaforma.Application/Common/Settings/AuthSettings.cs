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

    /// <summary>
    /// Hesap kilitleme eşiği (Sprint 31B-2): bu kadar ardışık başarısız login denemesinden sonra hesap
    /// <see cref="LockoutMinutes"/> dakika geçici olarak kilitlenir. Varsayılan 5.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Geçici kilit süresi (dakika). Varsayılan 15.</summary>
    public int LockoutMinutes { get; set; } = 15;
}
