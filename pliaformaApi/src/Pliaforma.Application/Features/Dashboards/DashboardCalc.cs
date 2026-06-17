using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Dashboards;

internal static class DashboardCalc
{
    public static AttendanceBreakdownDto Attendance(IEnumerable<AttendanceStatus> statuses)
    {
        int present = 0, absent = 0, excused = 0, late = 0, total = 0;
        foreach (var s in statuses)
        {
            total++;
            switch (s)
            {
                case AttendanceStatus.Present: present++; break;
                case AttendanceStatus.Absent: absent++; break;
                case AttendanceStatus.Excused: excused++; break;
                case AttendanceStatus.LateCancel: late++; break;
            }
        }
        return new AttendanceBreakdownDto(present, absent, excused, late, total);
    }

    /// <summary>
    /// Hoca kazanç TAHMİNİ (ödemeyle bire bir eşleşmez). Yalnızca o ay yapılmış (Cancelled değil, StartAt
    /// geçmiş) seanslara göre. PerSession → ders sayısı × rate; PerHour → saat × rate;
    /// PerStudentPerSession → katılım (Present) × rate. Bilinmeyen/gelir-paylaşımı tipi → null + not.
    /// </summary>
    public static (decimal? Earning, string Note) EstimateEarning(
        RateType rateType, decimal rate, int heldSessions, double heldHours, int presentCount)
    {
        const string caveat = " (tahmini; ödemeyle bire bir eşleşmeyebilir)";
        return rateType switch
        {
            RateType.PerSession => (heldSessions * rate, $"PerSession: {heldSessions} ders × {rate}{caveat}"),
            RateType.PerHour => ((decimal)heldHours * rate, $"PerHour: {heldHours:0.##} saat × {rate}{caveat}"),
            RateType.PerStudentPerSession => (presentCount * rate, $"PerStudentPerSession: {presentCount} katılım × {rate}{caveat}"),
            _ => (null, "Gelir paylaşımı/yüzde tipi ücret modellenmediğinden kazanç bu sürümde hesaplanmadı (ayrı sprintte)."),
        };
    }
}
