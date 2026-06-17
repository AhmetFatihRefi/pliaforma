namespace Pliaforma.Domain.Enums;

/// <summary>
/// Bir rezervasyon (ClassBooking) için yoklama sonucu. Yoklama yalnızca başlamış/bitmiş seanslarda ve
/// aktif (Booked) rezervasyonlar için alınır.
/// </summary>
public enum AttendanceStatus
{
    /// <summary>Geldi.</summary>
    Present = 0,

    /// <summary>Gelmedi (mazeretsiz).</summary>
    Absent = 1,

    /// <summary>Mazeretli gelmedi. İsteğe bağlı telafi kredisi verilebilir.</summary>
    Excused = 2,

    /// <summary>Geç iptal. İsteğe bağlı telafi kredisi verilebilir (otomatik politika yok).</summary>
    LateCancel = 3,
}
