using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

/// <summary>Öğrenci kayıtlarına erişim/yönetim yetki kuralları.</summary>
internal static class StudentAccess
{
    /// <summary>
    /// Owner/StudioAdmin tüm öğrencileri yönetir; Instructor yalnızca KENDİ oluşturduğunu
    /// (CreatedByUserId == currentUser). Diğer roller yönetemez.
    /// </summary>
    public static bool CanManage(ICurrentUserService user, StudentProfile student)
    {
        var role = user.StudioRole;
        if (role is StudioRole.Owner or StudioRole.StudioAdmin)
            return true;
        if (role == StudioRole.Instructor)
            return student.CreatedByUserId == user.UserId;
        return false;
    }

    public static void EnsureCanManage(ICurrentUserService user, StudentProfile student)
    {
        if (!CanManage(user, student))
            throw new ForbiddenException("Bu öğrenci üzerinde yetkiniz yok.");
    }

    /// <summary>Hassas sağlık notunu görme yetkisi: yöneten roller + öğrencinin kendisi.</summary>
    public static bool CanSeeHealth(ICurrentUserService user, StudentProfile student)
    {
        if (CanManage(user, student))
            return true;
        return user.UserId is { } uid && student.UserId == uid;
    }
}
