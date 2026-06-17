using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.MakeupCredits;

/// <summary>Telafi kredisi yönetiminde rol/erişim kuralları.</summary>
internal static class MakeupCreditAccess
{
    /// <summary>
    /// Owner/StudioAdmin tüm öğrencileri yönetir. Instructor yalnızca KENDİ oluşturduğu ya da
    /// KENDİ seanslarında rezervasyonu olan öğrencinin kredilerini yönetir/görür. Diğerleri yasak.
    /// </summary>
    public static async Task EnsureCanManageAsync(
        IAppDbContext db, ICurrentUserService user, Guid studioId, StudentProfile student, CancellationToken ct)
    {
        var role = user.StudioRole;
        if (role is StudioRole.Owner or StudioRole.StudioAdmin)
            return;

        if (role == StudioRole.Instructor)
        {
            var userId = user.RequireUserId();
            if (student.CreatedByUserId == userId)
                return;

            var ownProfileId = await db.InstructorProfiles
                .Where(p => p.StudioId == studioId && p.UserId == userId)
                .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            if (ownProfileId is { } own)
            {
                var hasBooking = await db.ClassBookings.AnyAsync(
                    b => b.StudioId == studioId && b.StudentProfileId == student.Id
                         && b.ClassSession!.InstructorProfileId == own, ct);
                if (hasBooking)
                    return;
            }
            throw new ForbiddenException("Bu öğrencinin telafi kredilerini yönetme yetkiniz yok.");
        }

        throw new ForbiddenException("Telafi kredisi yönetme yetkiniz yok.");
    }
}
