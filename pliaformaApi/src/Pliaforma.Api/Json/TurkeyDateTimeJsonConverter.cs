using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pliaforma.Application.Common.Time;

namespace Pliaforma.Api.Json;

/// <summary>
/// Giden TÜM <see cref="DateTime"/> değerlerini Türkiye saatine (Europe/Istanbul) normalize eder; böylece
/// saat çevirimi TEK yerde, backend'de yapılır ve frontend hiçbir TZ matematiği yapmaz (yalnız string gösterir).
///
/// Davranış (Kind'a göre):
///  - <b>Utc</b> (audit/işlem damgaları: CreatedAt, PaidAt, …) → Türkiye yerel saatine çevrilir.
///  - <b>Unspecified/Local</b> (ders/kapanış StartAt — zaten stüdyo-yerel duvar saati) → OLDUĞU GİBİ yazılır
///    (çift kayma olmaz). Npgsql, "timestamp with time zone" alanları Kind=Utc, "without" alanları Kind=Unspecified
///    döndürdüğü için bu ayrım güvenilirdir.
///
/// Çıktı offset taşımaz ("yyyy-MM-ddTHH:mm:ss") → frontend slice(11,16) doğrudan Türkiye saatini gösterir.
/// Giriş (Read) varsayılan davranışı korur (offset'siz → Unspecified duvar saati; create/reschedule bozulmaz).
/// DateOnly/TimeOnly bu converter'dan ETKİLENMEZ (yalnız-tarih alanları olduğu gibi kalır).
/// </summary>
public sealed class TurkeyDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var local = value.Kind == DateTimeKind.Utc ? TurkeyTime.FromUtc(value) : value;
        writer.WriteStringValue(local.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
    }
}
