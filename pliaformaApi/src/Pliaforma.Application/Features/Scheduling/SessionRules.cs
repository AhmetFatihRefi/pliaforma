using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>Ders seansı iş kuralları: stüdyo-yerel "şimdi", çakışma, müsaitlik penceresi.</summary>
internal static class SessionRules
{
    /// <summary>
    /// Stüdyo zaman dilimine göre şimdiki yerel saat (duvar saati, Unspecified). Geçersiz TZ → UTC.
    /// </summary>
    public static async Task<DateTime> StudioNowAsync(IAppDbContext db, IDateTime clock, Guid studioId, CancellationToken ct)
    {
        var tzId = await db.Studios.Where(s => s.Id == studioId).Select(s => s.TimeZoneId).FirstAsync(ct);
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.Utc; }
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, tz), DateTimeKind.Unspecified);
    }

    /// <summary>Verilen aralık için aktif (Scheduled) çakışan seans var mı? (excludeId hariç).</summary>
    public static Task<bool> HasConflictAsync(
        IAppDbContext db, Guid studioId, Guid instructorProfileId, DateTime startAt, DateTime endAt,
        Guid? excludeId, CancellationToken ct) =>
        db.ClassSessions.AnyAsync(
            s => s.StudioId == studioId
                 && s.InstructorProfileId == instructorProfileId
                 && s.Status == ClassSessionStatus.Scheduled
                 && (excludeId == null || s.Id != excludeId)
                 && s.StartAt < endAt && startAt < s.EndAt,
            ct);

    /// <summary>
    /// Müsaitlik penceresi uygunluğu. Hoca için hiç availability yoksa: Owner/StudioAdmin serbest,
    /// Instructor reddedilir (önce müsaitlik tanımlamalı). Availability varsa seans bir pencereye sığmalı.
    /// reason out-param: uymuyorsa neden ("outside-availability" / "needs-availability").
    /// </summary>
    public static async Task<bool> FitsAvailabilityAsync(
        IAppDbContext db, ICurrentUserService user, Guid studioId, Guid instructorProfileId,
        DateTime startAt, DateTime endAt, CancellationToken ct)
    {
        var avails = await db.InstructorAvailabilities
            .Where(a => a.StudioId == studioId && a.InstructorProfileId == instructorProfileId && a.IsActive)
            .ToListAsync(ct);

        if (avails.Count == 0)
            return user.StudioRole != StudioRole.Instructor; // Owner/Admin serbest, Instructor değil

        var day = startAt.DayOfWeek;
        var s = TimeOnly.FromDateTime(startAt);
        var e = TimeOnly.FromDateTime(endAt);
        return avails.Any(a => a.DayOfWeek == day && a.StartTime <= s && e <= a.EndTime);
    }

    /// <summary>Tek seans oluşturma/güncellemede tüm kuralları zorlar (hata fırlatır).</summary>
    public static async Task EnsureValidAsync(
        IAppDbContext db, ICurrentUserService user, IDateTime clock, Guid studioId, Guid instructorProfileId,
        DateTime startAt, DateTime endAt, Guid? excludeId, CancellationToken ct)
    {
        var nowLocal = await StudioNowAsync(db, clock, studioId, ct);
        if (startAt < nowLocal)
            throw new BusinessRuleException("Geçmiş bir zamana ders oluşturulamaz.");

        if (await HasConflictAsync(db, studioId, instructorProfileId, startAt, endAt, excludeId, ct))
            throw new BusinessRuleException("Hoca bu zaman aralığında başka bir derse atanmış.");

        if (!await FitsAvailabilityAsync(db, user, studioId, instructorProfileId, startAt, endAt, ct))
        {
            var hasAny = await db.InstructorAvailabilities
                .AnyAsync(a => a.StudioId == studioId && a.InstructorProfileId == instructorProfileId && a.IsActive, ct);
            throw new BusinessRuleException(hasAny
                ? "Ders, hocanın müsaitlik penceresi dışında."
                : "Bu hoca için önce müsaitlik tanımlanmalı.");
        }
    }
}
