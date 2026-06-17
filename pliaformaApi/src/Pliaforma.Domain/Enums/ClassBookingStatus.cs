namespace Pliaforma.Domain.Enums;

/// <summary>Bir ders rezervasyonunun durumu. Aktif = Booked.</summary>
public enum ClassBookingStatus
{
    /// <summary>Aktif rezervasyon (kapasite tüketir).</summary>
    Booked = 0,

    /// <summary>Öğrenci tarafından iptal edildi.</summary>
    CancelledByStudent = 1,

    /// <summary>Stüdyo (Owner/StudioAdmin/Instructor) tarafından iptal edildi.</summary>
    CancelledByStudio = 2,
}
