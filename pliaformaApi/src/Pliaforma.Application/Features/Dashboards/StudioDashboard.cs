using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Contracts;
using Pliaforma.Application.Features.Packages;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Dashboards;

/// <summary>Stüdyo geneli aylık dashboard (Owner/StudioAdmin). Read-only; tek ay aralığı.</summary>
public sealed record GetStudioDashboardQuery(string? Month) : IQuery<StudioDashboardDto>;

public sealed class GetStudioDashboardHandler : IQueryHandler<GetStudioDashboardQuery, StudioDashboardDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetStudioDashboardHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<StudioDashboardDto> Handle(GetStudioDashboardQuery request, CancellationToken ct)
    {
        var sid = _currentUser.RequireStudioId();
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, sid, ct);
        var today = DateOnly.FromDateTime(nowLocal);
        var p = ReportPeriod.Resolve(request.Month, nowLocal);

        // --- Öğrenciler ---
        var students = await _db.StudentProfiles.AsNoTracking().Where(s => s.StudioId == sid)
            .Select(s => new { s.Status, s.CreatedAt }).ToListAsync(ct);
        var totalStudents = students.Count;
        var activeStudents = students.Count(s => s.Status == StudentStatus.Active);
        var guestStudents = students.Count(s => s.Status == StudentStatus.Guest);
        var newStudents = students.Count(s => s.CreatedAt >= p.UtcStart && s.CreatedAt < p.UtcEnd);

        var totalInstructors = await _db.InstructorProfiles.AsNoTracking().CountAsync(i => i.StudioId == sid, ct);

        // --- Seanslar (bu ay) ---
        var sessions = await _db.ClassSessions.AsNoTracking()
            .Where(s => s.StudioId == sid && s.StartAt >= p.LocalStart && s.StartAt < p.LocalEnd)
            .Select(s => new { s.Status, s.StartAt }).ToListAsync(ct);
        var sessionsThisMonth = sessions.Count;
        var cancelledSessions = sessions.Count(s => s.Status == ClassSessionStatus.Cancelled);
        var completedOrPast = sessions.Count(s => s.Status != ClassSessionStatus.Cancelled && s.StartAt <= nowLocal);

        var bookingsThisMonth = await _db.ClassBookings.AsNoTracking().CountAsync(
            b => b.StudioId == sid && b.Status == ClassBookingStatus.Booked
                 && b.ClassSession!.StartAt >= p.LocalStart && b.ClassSession.StartAt < p.LocalEnd, ct);

        // --- Yoklama (bu ay, session StartAt'e göre) ---
        var attStatuses = await (
            from a in _db.Attendances.AsNoTracking()
            join s in _db.ClassSessions on a.ClassSessionId equals s.Id
            where a.StudioId == sid && s.StartAt >= p.LocalStart && s.StartAt < p.LocalEnd
            select a.Status).ToListAsync(ct);
        var attendance = DashboardCalc.Attendance(attStatuses);

        // --- Ödemeler (Sprint 45: gecikme/kalan LINE-bazlı; Payment.DueDate/Status tabanlı ikinci gecikme kavramı YOK) ---
        // "Ödenen" = bu ay fiilen ALINAN (Paid) ödemeler (PaidAt). "Kalan/Gecikmiş" = aktif line'ların anlık borç durumu.
        // Not: dashboard tek-currency toplar (N5 — TRY varsayımı, mevcut davranış korunur).
        var paidRows = await _db.Payments.AsNoTracking()
            .Where(p2 => p2.StudioId == sid && p2.Status == PaymentStatus.Paid
                && p2.PaidAt != null && p2.PaidAt >= p.UtcStart && p2.PaidAt < p.UtcEnd)
            .Select(p2 => p2.Amount).ToListAsync(ct);
        var paidAmt = paidRows.Sum();
        var paidCnt = paidRows.Count;

        var debtLines = await ContractLineQuery.BuildAsync(_db,
            _db.StudentContractLines.AsNoTracking().Where(l => l.StudioId == sid && l.Status == StudentContractLineStatus.Active), today, ct);
        var overdueAmt = debtLines.Sum(l => l.OverdueAmount);
        var overdueCnt = debtLines.Count(l => l.IsOverdue);
        var pendingAmt = debtLines.Sum(l => l.Remaining - l.OverdueAmount);   // kalan ama vadesi geçmemiş
        var pendingCnt = debtLines.Count(l => l.Remaining > 0m && !l.IsOverdue);
        var payments = new PaymentTotalsDto(paidAmt, pendingAmt, overdueAmt, paidCnt, pendingCnt, overdueCnt);

        // --- Paketler (effectiveStatus dikkate alınır) ---
        var pkgRows = await _db.StudentPackages.AsNoTracking().Where(sp => sp.StudioId == sid)
            .Select(sp => new { sp.Status, sp.InitialCreditCount, sp.CurrentCreditBalance, sp.ValidUntil }).ToListAsync(ct);
        var activePackages = pkgRows.Count(x => PackageRules.Effective(x.Status, x.ValidUntil, today) == StudentPackageStatus.Active);
        var usedCredits = pkgRows.Sum(x => Math.Max(x.InitialCreditCount - x.CurrentCreditBalance, 0));
        var remainingCredits = pkgRows.Where(x => PackageRules.Effective(x.Status, x.ValidUntil, today) == StudentPackageStatus.Active)
            .Sum(x => x.CurrentCreditBalance);
        var packages = new PackageTotalsDto(activePackages, usedCredits, remainingCredits);

        // --- Telafi kredileri ---
        var mkRows = await _db.MakeupCreditTransactions.AsNoTracking().Where(t => t.StudioId == sid)
            .Select(t => new { t.Amount, t.Type, t.CreatedAt }).ToListAsync(ct);
        var outstanding = mkRows.Sum(x => x.Amount);
        var grantedThisMonth = mkRows.Where(x => x.Type == MakeupCreditTransactionType.Granted && x.CreatedAt >= p.UtcStart && x.CreatedAt < p.UtcEnd).Sum(x => x.Amount);
        var usedThisMonth = mkRows.Where(x => x.Type == MakeupCreditTransactionType.Used && x.CreatedAt >= p.UtcStart && x.CreatedAt < p.UtcEnd).Sum(x => -x.Amount);
        var makeup = new MakeupTotalsDto(outstanding, grantedThisMonth, usedThisMonth);

        // --- Yaklaşan ---
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var todayEnd = today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var todaySessions = await _db.ClassSessions.AsNoTracking().CountAsync(
            s => s.StudioId == sid && s.Status == ClassSessionStatus.Scheduled && s.StartAt >= todayStart && s.StartAt < todayEnd, ct);
        var next7 = await _db.ClassSessions.AsNoTracking().CountAsync(
            s => s.StudioId == sid && s.Status == ClassSessionStatus.Scheduled && s.StartAt > nowLocal && s.StartAt <= nowLocal.AddDays(7), ct);
        var upcoming = new UpcomingDto(todaySessions, next7);

        return new StudioDashboardDto(
            p.Label, totalStudents, activeStudents, guestStudents, newStudents, totalInstructors,
            sessionsThisMonth, completedOrPast, cancelledSessions, bookingsThisMonth,
            attendance, payments, packages, makeup, upcoming);
    }
}
