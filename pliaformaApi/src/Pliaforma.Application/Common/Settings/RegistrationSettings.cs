namespace Pliaforma.Application.Common.Settings;

/// <summary>
/// Herkese açık kayıt/stüdyo oluşturma davranışı (config "Registration" bölümü). Sprint 49: tek-salon
/// pilot dağıtımında dışarıdan yeni stüdyo oluşturmayı kapatmak için kullanılır.
/// </summary>
public sealed class RegistrationSettings
{
    public const string SectionName = "Registration";

    /// <summary>
    /// Herkese açık stüdyo kaydı (<c>register-studio</c>) etkin mi?
    /// <para>true (VARSAYILAN / tam SaaS): herkes yeni stüdyo + Owner hesabı oluşturabilir (mevcut davranış;
    /// dev/test/SaaS bozulmaz).</para>
    /// <para>false (pilot / tek-salon): <c>register-studio</c> 403 döner; yeni stüdyo yalnız operatör tarafından
    /// açılır. Login, davet-kabul, mevcut Owner/Admin ve stüdyosuz hesap (register) akışları ETKİLENMEZ.</para>
    /// Production dağıtımında güvenli varsayılan için <c>docker-compose.prod.yml</c> bu değeri
    /// <c>Registration__StudioRegistrationEnabled=false</c> olarak geçer (operatör açıkça açmadıkça kapalı).
    /// </summary>
    public bool StudioRegistrationEnabled { get; set; } = true;
}
