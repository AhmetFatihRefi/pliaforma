using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>Müsaitlik/seans işlemlerinde hoca bazlı yetki kuralları.</summary>
internal static class SchedulingAccess
{
    /// <summary>Owner/StudioAdmin tüm hocaları yönetir; Instructor yalnızca KENDİ profilini.</summary>
    public static void EnsureCanManage(ICurrentUserService user, InstructorProfile profile)
    {
        var role = user.StudioRole;
        if (role is StudioRole.Owner or StudioRole.StudioAdmin)
            return;
        if (role == StudioRole.Instructor && profile.UserId == user.UserId)
            return;
        throw new ForbiddenException("Bu hoca için işlem yetkiniz yok.");
    }

    /// <summary>Aktif stüdyoda hoca profilini yükler ve yönetim yetkisini doğrular.</summary>
    public static async Task<InstructorProfile> RequireManageableInstructorAsync(
        IAppDbContext db, ICurrentUserService user, Guid studioId, Guid instructorProfileId, CancellationToken ct)
    {
        var profile = await db.InstructorProfiles
            .FirstOrDefaultAsync(p => p.Id == instructorProfileId && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Hoca profili", instructorProfileId);

        EnsureCanManage(user, profile);
        return profile;
    }

    /// <summary>Instructor ise kendi hoca profili Id'si (liste scope'u için); değilse null.</summary>
    public static async Task<Guid?> CurrentInstructorProfileIdAsync(
        IAppDbContext db, ICurrentUserService user, Guid studioId, CancellationToken ct)
    {
        if (user.StudioRole != StudioRole.Instructor)
            return null;

        var userId = user.RequireUserId();
        return await db.InstructorProfiles
            .Where(p => p.StudioId == studioId && p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Ders onayı yetkisi (Sprint 25): Owner/StudioAdmin her dersi onaylayabilir; Instructor yalnızca
    /// PLANLANAN veya FİİLÎ hocası KENDİSİ olan dersi. Başka hocanın dersi → 403.
    /// </summary>
    public static async Task EnsureCanConfirmSessionAsync(
        IAppDbContext db, ICurrentUserService user, Guid studioId, ClassSession session, CancellationToken ct)
    {
        var role = user.StudioRole;
        if (role is StudioRole.Owner or StudioRole.StudioAdmin)
            return;

        if (role == StudioRole.Instructor)
        {
            var myProfileId = await CurrentInstructorProfileIdAsync(db, user, studioId, ct);
            if (myProfileId is { } mine
                && (session.InstructorProfileId == mine || session.ActualInstructorProfileId == mine))
                return;
        }

        throw new ForbiddenException("Bu dersi onaylama yetkiniz yok.");
    }
}
