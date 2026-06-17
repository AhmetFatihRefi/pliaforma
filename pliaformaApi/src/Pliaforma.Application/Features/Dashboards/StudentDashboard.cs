using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Payments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Dashboards;

/// <summary>Öğrenci aylık dashboard'unu kuran ortak çekirdek (self + staff view paylaşır).</summary>
internal static class StudentDashboardBuilder
{
    public static async Task<StudentDashboardDto> BuildAsync(
        IAppDbContext db, IDateTime clock, MakeupCreditService credits, Guid sid, Guid studentProfileId,
        string studentName, string status, string? month, bool includeFinance, CancellationToken ct)
    {
        var nowLocal = await SessionRules.StudioNowAsync(db, clock, sid, ct);
        var today = DateOnly.FromDateTime(nowLocal);
        var p = ReportPeriod.Resolve(month, nowLocal);

        // Yaklaşan rezervasyonlar
        var upcoming = await db.ClassBookings.AsNoTracking()
            .Where(b => b.StudioId == sid && b.StudentProfileId == studentProfileId
                        && b.Status == ClassBookingStatus.Booked && b.ClassSession!.StartAt > nowLocal)
            .OrderBy(b => b.ClassSession!.StartAt).Take(10)
            .Select(b => new StudentMiniBookingDto(b.Id, b.ClassSessionId, b.ClassSession!.ClassType!.Name,
                b.ClassSession.InstructorProfile!.DisplayName, b.ClassSession.StartAt, b.ClassSession.EndAt))
            .ToListAsync(ct);

        // Aylık katılım
        var attStatuses = await (
            from a in db.Attendances.AsNoTracking()
            join s in db.ClassSessions on a.ClassSessionId equals s.Id
            where a.StudioId == sid && a.StudentProfileId == studentProfileId && s.StartAt >= p.LocalStart && s.StartAt < p.LocalEnd
            select a.Status).ToListAsync(ct);
        var attendance = DashboardCalc.Attendance(attStatuses);

        // Paket özeti (effectiveStatus)
        var pkgRows = await db.StudentPackages.AsNoTracking()
            .Where(sp => sp.StudioId == sid && sp.StudentProfileId == studentProfileId)
            .Select(sp => new { sp.Status, sp.CurrentCreditBalance, sp.ValidUntil }).ToListAsync(ct);
        var activePkgs = pkgRows.Where(x => PackageRulesEffective(x.Status, x.ValidUntil, today) == StudentPackageStatus.Active).ToList();
        var expiries = activePkgs.Where(x => x.ValidUntil != null).Select(x => x.ValidUntil!.Value).ToList();
        DateOnly? nearestExpiry = expiries.Count > 0 ? expiries.Min() : null;
        var pkgSummary = new StudentPackageSummaryDto(activePkgs.Count, activePkgs.Sum(x => x.CurrentCreditBalance), nearestExpiry);

        // Ödeme özeti (yalnızca finans görünür ise)
        StudentPaymentSummaryDto? paySummary = null;
        if (includeFinance)
        {
            var payRows = await db.Payments.AsNoTracking()
                .Where(pm => pm.StudioId == sid && pm.StudentProfileId == studentProfileId)
                .Select(pm => new { pm.Amount, pm.Status, pm.PaidAt, pm.DueDate }).ToListAsync(ct);
            decimal pending = 0, overdue = 0; DateOnly? nextDue = null;
            foreach (var r in payRows)
            {
                var eff = PaymentMapping.Effective(r.Status, r.PaidAt, r.DueDate, today);
                if (eff == PaymentStatus.Pending) pending += r.Amount;
                else if (eff == PaymentStatus.Overdue) overdue += r.Amount;
                if (eff is PaymentStatus.Pending or PaymentStatus.Overdue && (nextDue is null || r.DueDate < nextDue))
                    nextDue = r.DueDate;
            }
            paySummary = new StudentPaymentSummaryDto(pending, overdue, nextDue);
        }

        var makeupBalance = await credits.GetBalanceAsync(sid, studentProfileId, ct);

        var recentMakeup = (await db.MakeupCreditTransactions.AsNoTracking()
            .Where(t => t.StudioId == sid && t.StudentProfileId == studentProfileId)
            .OrderByDescending(t => t.CreatedAt).Take(5)
            .Select(t => new { t.Id, t.Amount, t.Type, t.Reason, t.CreatedAt }).ToListAsync(ct))
            .Select(t => new StudentMiniMakeupDto(t.Id, t.Amount, t.Type.ToString(), t.Reason.ToString(), t.CreatedAt)).ToList();

        var recentAttendance = (await (
            from a in db.Attendances.AsNoTracking()
            join s in db.ClassSessions on a.ClassSessionId equals s.Id
            where a.StudioId == sid && a.StudentProfileId == studentProfileId
            orderby s.StartAt descending
            select new { a.Id, ClassTypeName = s.ClassType!.Name, s.StartAt, a.Status })
            .Take(5).ToListAsync(ct))
            .Select(a => new StudentMiniAttendanceDto(a.Id, a.ClassTypeName, a.StartAt, a.Status.ToString())).ToList();

        var recentBookings = await db.ClassBookings.AsNoTracking()
            .Where(b => b.StudioId == sid && b.StudentProfileId == studentProfileId)
            .OrderByDescending(b => b.ClassSession!.StartAt).Take(5)
            .Select(b => new StudentMiniBookingDto(b.Id, b.ClassSessionId, b.ClassSession!.ClassType!.Name,
                b.ClassSession.InstructorProfile!.DisplayName, b.ClassSession.StartAt, b.ClassSession.EndAt))
            .ToListAsync(ct);

        return new StudentDashboardDto(
            p.Label, studentProfileId, studentName, status, upcoming, attendance, pkgSummary, paySummary,
            makeupBalance, recentMakeup, recentAttendance, recentBookings);
    }

    private static StudentPackageStatus PackageRulesEffective(StudentPackageStatus s, DateOnly? validUntil, DateOnly today) =>
        Packages.PackageRules.Effective(s, validUntil, today);
}

// ---- Öğrenci self ----
public sealed record GetMyStudentDashboardQuery(string? Month) : IQuery<StudentDashboardDto>;

public sealed class GetMyStudentDashboardHandler : IQueryHandler<GetMyStudentDashboardQuery, StudentDashboardDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;
    public GetMyStudentDashboardHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, MakeupCreditService credits)
    { _db = db; _currentUser = currentUser; _clock = clock; _credits = credits; }

    public async Task<StudentDashboardDto> Handle(GetMyStudentDashboardQuery request, CancellationToken ct)
    {
        var sid = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var student = await _db.StudentProfiles.AsNoTracking()
            .Where(s => s.StudioId == sid && s.UserId == userId)
            .Select(s => new { s.Id, s.FullName, s.Status }).FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException("Bu stüdyoda öğrenci profiliniz yok.");

        return await StudentDashboardBuilder.BuildAsync(_db, _clock, _credits, sid, student.Id, student.FullName,
            student.Status.ToString(), request.Month, includeFinance: true, ct);
    }
}

// ---- Staff: bir öğrencinin dashboard'u (Instructor finans göremez) ----
public sealed record GetStaffStudentDashboardQuery(Guid StudentProfileId, string? Month) : IQuery<StudentDashboardDto>;

public sealed class GetStaffStudentDashboardHandler : IQueryHandler<GetStaffStudentDashboardQuery, StudentDashboardDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;
    public GetStaffStudentDashboardHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, MakeupCreditService credits)
    { _db = db; _currentUser = currentUser; _clock = clock; _credits = credits; }

    public async Task<StudentDashboardDto> Handle(GetStaffStudentDashboardQuery request, CancellationToken ct)
    {
        var sid = _currentUser.RequireStudioId();
        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == sid, ct)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        // Erişim: Owner/Admin tümü; Instructor yalnızca kendi oluşturduğu/seansında olan.
        await MakeupCreditAccess.EnsureCanManageAsync(_db, _currentUser, sid, student, ct);

        // Finans yalnızca Owner/StudioAdmin'e (Instructor görmez).
        var includeFinance = _currentUser.StudioRole is StudioRole.Owner or StudioRole.StudioAdmin;

        return await StudentDashboardBuilder.BuildAsync(_db, _clock, _credits, sid, student.Id, student.FullName,
            student.Status.ToString(), request.Month, includeFinance, ct);
    }
}
