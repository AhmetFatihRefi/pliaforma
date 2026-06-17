namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Hatırlatma worker'ının yapılandırma özeti (gözlemlenebilirlik için, READ-ONLY). Worker ile aynı
/// appsettings anahtarlarını okur; Infrastructure'da IConfiguration üzerinden uygulanır (Application
/// katmanı IConfiguration'a bağımlı olmaz).
/// </summary>
public interface IReminderWorkerInfo
{
    bool WorkerEnabled { get; }
    int IntervalSeconds { get; }
}
