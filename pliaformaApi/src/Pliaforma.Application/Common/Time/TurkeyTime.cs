namespace Pliaforma.Application.Common.Time;

/// <summary>
/// Türkiye saati (Europe/Istanbul) — uygulamanın TEK ve KESİN yerel zaman ekseni. Türkiye 2016'dan beri
/// sabit UTC+3'tür (yaz/kış saati YOK). Depolama UTC kalır; bu yardımcı yalnızca <b>yerel saate çevirim</b>
/// içindir (JSON çıktısı + "şimdi" hesapları). Zone bir kez çözülür ve <b>asla UTC'ye düşmez</b>:
/// IANA "Europe/Istanbul" → Windows "Turkey Standard Time" → son çare sabit +03:00.
/// </summary>
public static class TurkeyTime
{
    public static readonly TimeZoneInfo Zone = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* sonraki id'yi dene */ }
        }
        // Hiçbir TZ veritabanı yoksa: Türkiye sabit UTC+3 (DST yok) — yine de UTC'ye DÜŞME.
        return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "Türkiye Saati", "Türkiye Saati");
    }

    /// <summary>UTC bir an'ı Türkiye yerel duvar saatine çevirir (Kind=Unspecified — offset taşımaz).</summary>
    public static DateTime FromUtc(DateTime utc)
    {
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(asUtc, Zone), DateTimeKind.Unspecified);
    }

    /// <summary>Şimdiki Türkiye yerel saati (duvar saati, Unspecified).</summary>
    public static DateTime Now(DateTime utcNow) => FromUtc(utcNow);
}
