namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Sprint 46 — Pilot Simple People Mode bayrağı (READ-ONLY, global). Config anahtarı
/// <c>Pilot:SimplePeopleMode</c> (env <c>Pilot__SimplePeopleMode=true</c>); default false. Infrastructure'da
/// IConfiguration üzerinden uygulanır (Application katmanı IConfiguration'a bağımlı olmaz). Geçici pilot katmanı;
/// açıkken: hoca ad-soyad öğrenci ekleyebilir, ad-soyad öğrenci gizli pilot line ile Regular derse eklenebilir.
/// </summary>
public interface IPilotModeInfo
{
    bool SimplePeopleModeEnabled { get; }
}
