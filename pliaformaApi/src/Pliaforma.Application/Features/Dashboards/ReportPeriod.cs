namespace Pliaforma.Application.Features.Dashboards;

/// <summary>
/// Rapor/dashboard ay aralığı. "YYYY-MM" verilirse o ay; geçersiz/boşsa stüdyo-yerel içinde bulunulan ay.
/// Aralık her zaman **tek ay** olduğundan geniş aralık riski yoktur (Sprint 8 kural 7).
/// - Local (Unspecified): stüdyo-yerel duvar saati alanları için (ClassSession.StartAt).
/// - Utc: audit alanları için (CreatedAt/PaidAt — UTC timestamptz).
/// - Date (DateOnly): tarih alanları için (Payment.DueDate, StudentPackage.ValidUntil).
/// </summary>
internal readonly record struct ReportPeriod(
    DateTime LocalStart, DateTime LocalEnd,
    DateTime UtcStart, DateTime UtcEnd,
    DateOnly StartDate, DateOnly EndDate,
    string Label)
{
    public static ReportPeriod Resolve(string? month, DateTime nowLocal)
    {
        int y = nowLocal.Year, m = nowLocal.Month;
        if (!string.IsNullOrWhiteSpace(month) && month.Length == 7 && month[4] == '-'
            && int.TryParse(month[..4], out var py) && int.TryParse(month[5..], out var pm) && pm is >= 1 and <= 12)
        {
            y = py;
            m = pm;
        }

        var local = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var date = new DateOnly(y, m, 1);
        return new ReportPeriod(local, local.AddMonths(1), utc, utc.AddMonths(1), date, date.AddMonths(1), $"{y:D4}-{m:D2}");
    }
}
