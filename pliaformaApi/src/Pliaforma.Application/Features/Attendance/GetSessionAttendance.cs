using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>Seans yoklama görünümünü kuran ortak yardımcı (query + mark sonrası dönüş paylaşır).</summary>
internal static class AttendanceViews
{
    public static async Task<SessionAttendanceDto> BuildSessionViewAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, ClassSession session, bool includePackages, CancellationToken ct)
    {
        var classTypeName = await db.ClassTypes.Where(c => c.Id == session.ClassTypeId).Select(c => c.Name).FirstAsync(ct);
        var instructorName = await db.InstructorProfiles.Where(p => p.Id == session.InstructorProfileId).Select(p => p.DisplayName).FirstAsync(ct);

        var nowLocal = await SessionRules.StudioNowAsync(db, clock, studioId, ct);
        var canMark = session.StartAt <= nowLocal && session.Status != ClassSessionStatus.Cancelled;

        // Yalnızca aktif (Booked) rezervasyonlar yoklama listesine gelir.
        var baseItems = await db.ClassBookings
            .Where(b => b.StudioId == studioId && b.ClassSessionId == session.Id && b.Status == ClassBookingStatus.Booked)
            .OrderBy(b => b.StudentProfile!.FullName)
            .Select(b => new
            {
                b.Id,
                b.StudentProfileId,
                Name = b.StudentProfile!.FullName,
                Status = db.Attendances.Where(a => a.ClassBookingId == b.Id).Select(a => (AttendanceStatus?)a.Status).FirstOrDefault(),
                Notes = db.Attendances.Where(a => a.ClassBookingId == b.Id).Select(a => a.Notes).FirstOrDefault(),
                CreditGranted = db.Attendances.Any(a => a.ClassBookingId == b.Id && a.MakeupCreditTransactionId != null),
                PackageConsumed = db.StudentPackageTransactions.Any(t => t.SourceClassBookingId == b.Id && t.Type == StudentPackageTransactionType.Used),
                MarkedAt = db.Attendances.Where(a => a.ClassBookingId == b.Id).Select(a => (DateTime?)a.MarkedAt).FirstOrDefault(),
            })
            .ToListAsync(ct);

        // Paket bilgisi YALNIZCA Owner/StudioAdmin'e (finans gizliliği — Instructor görmez).
        // Yalnızca KULLANILABİLİR paketler (süresi geçmemiş) listelenir → modalda expired seçilemez.
        var today = DateOnly.FromDateTime(nowLocal);
        var pkgByStudent = new Dictionary<Guid, List<StudentPackageBriefDto>>();
        if (includePackages && baseItems.Count > 0)
        {
            var studentIds = baseItems.Select(i => i.StudentProfileId).Distinct().ToList();
            var pkgs = await db.StudentPackages
                .Where(p => p.StudioId == studioId && studentIds.Contains(p.StudentProfileId)
                            && p.Status == StudentPackageStatus.Active && p.CurrentCreditBalance > 0
                            && (p.ValidUntil == null || p.ValidUntil >= today))
                .OrderBy(p => p.ValidUntil == null).ThenBy(p => p.ValidUntil).ThenBy(p => p.CreatedAt)
                .Select(p => new { p.StudentProfileId, Brief = new StudentPackageBriefDto(p.Id, p.Name, p.CurrentCreditBalance, p.ValidUntil) })
                .ToListAsync(ct);
            pkgByStudent = pkgs.GroupBy(x => x.StudentProfileId).ToDictionary(g => g.Key, g => g.Select(x => x.Brief).ToList());
        }

        var items = baseItems.Select(i => new AttendanceItemDto(
            i.Id, i.StudentProfileId, i.Name, i.Status, i.Notes, i.CreditGranted, i.PackageConsumed, i.MarkedAt,
            includePackages
                ? (pkgByStudent.TryGetValue(i.StudentProfileId, out var list) ? list : new List<StudentPackageBriefDto>())
                : null)).ToList();

        return new SessionAttendanceDto(session.Id, classTypeName, instructorName, session.StartAt, session.EndAt,
            session.Status, canMark, items);
    }

    /// <summary>Owner/StudioAdmin paket bilgisini görür; Instructor görmez.</summary>
    public static bool CanSeePackages(ICurrentUserService user) =>
        user.StudioRole is StudioRole.Owner or StudioRole.StudioAdmin;
}

/// <summary>Bir seansın yoklama listesi. Owner/StudioAdmin tüm seanslar; Instructor yalnızca kendi seansı.</summary>
public sealed record GetSessionAttendanceQuery(Guid SessionId) : IQuery<SessionAttendanceDto>;

public sealed class GetSessionAttendanceHandler : IQueryHandler<GetSessionAttendanceQuery, SessionAttendanceDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetSessionAttendanceHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<SessionAttendanceDto> Handle(GetSessionAttendanceQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.SessionId);

        // Instructor yalnızca kendi seansı (Owner/Admin tümü) → aksi 403.
        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, session.InstructorProfileId, cancellationToken);

        return await AttendanceViews.BuildSessionViewAsync(
            _db, _clock, studioId, session, AttendanceViews.CanSeePackages(_currentUser), cancellationToken);
    }
}
