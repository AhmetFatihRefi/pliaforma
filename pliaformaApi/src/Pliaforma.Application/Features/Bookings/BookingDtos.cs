using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Bookings;

/// <summary>Stüdyo/personel tarafı rezervasyon görünümü (öğrenci + seans bilgisiyle).</summary>
public sealed record ClassBookingDto(
    Guid Id,
    Guid ClassSessionId,
    Guid StudentProfileId,
    string StudentName,
    ClassBookingStatus Status,
    DateTime StartAt,
    DateTime EndAt,
    string ClassTypeName,
    string InstructorName,
    DateTime? CancelledAt);

/// <summary>Öğrencinin kendi rezervasyonu (yaklaşan).</summary>
public sealed record MyBookingDto(
    Guid BookingId,
    Guid ClassSessionId,
    string ClassTypeName,
    string InstructorName,
    DateTime StartAt,
    DateTime EndAt,
    ClassBookingStatus Status);

/// <summary>Öğrencinin rezerve edebileceği seans (doluluk + kendi rezervasyon durumu + makeup bilgisi).</summary>
public sealed record StudentSessionDto(
    Guid Id,
    string ClassTypeName,
    string InstructorName,
    DateTime StartAt,
    DateTime EndAt,
    int Capacity,
    int BookedCount,
    ClassSessionKind SessionKind,
    Guid? BookingId,
    bool AlreadyBooked)
{
    public int RemainingCapacity => Math.Max(Capacity - BookedCount, 0);
    public bool IsFull => BookedCount >= Capacity;

    /// <summary>Makeup seansı rezervasyonu 1 telafi kredisi gerektirir.</summary>
    public bool RequiresMakeupCredit => SessionKind == ClassSessionKind.Makeup;

    public bool CanBook => !AlreadyBooked && !IsFull;
}
