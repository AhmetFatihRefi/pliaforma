using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

// ---- Öğrencinin kendi yaklaşan rezervasyonları ----
public sealed record GetMyBookingsQuery : IQuery<IReadOnlyList<MyBookingDto>>;

public sealed class GetMyBookingsHandler : IQueryHandler<GetMyBookingsQuery, IReadOnlyList<MyBookingDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetMyBookingsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MyBookingDto>> Handle(GetMyBookingsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
            return [];

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);

        return await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.StudentProfileId == profileId
                        && b.Status == ClassBookingStatus.Booked && b.ClassSession!.StartAt >= nowLocal)
            .OrderBy(b => b.ClassSession!.StartAt)
            .Select(b => new MyBookingDto(
                b.Id, b.ClassSessionId, b.ClassSession!.ClassType!.Name, b.ClassSession.InstructorProfile!.DisplayName,
                b.ClassSession.StartAt, b.ClassSession.EndAt, b.Status))
            .ToListAsync(cancellationToken);
    }
}

// ---- Öğrencinin rezerve edebileceği yaklaşan seanslar ----
public sealed record GetStudentSessionsQuery(DateOnly? From, DateOnly? To, Guid? ClassTypeId)
    : IQuery<IReadOnlyList<StudentSessionDto>>;

public sealed class GetStudentSessionsHandler : IQueryHandler<GetStudentSessionsQuery, IReadOnlyList<StudentSessionDto>>
{
    private const int MaxRangeDays = 90;
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetStudentSessionsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<StudentSessionDto>> Handle(GetStudentSessionsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId && s.Status == StudentStatus.Active)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("Bu stüdyoda aktif bir öğrenci profiliniz yok.");

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);

        var from = request.From?.ToDateTime(TimeOnly.MinValue) ?? nowLocal;
        var to = (request.To ?? DateOnly.FromDateTime(nowLocal).AddDays(30)).AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (to.Date.Subtract(from.Date).TotalDays > MaxRangeDays)
            throw new BusinessRuleException($"Tarih aralığı en fazla {MaxRangeDays} gün olabilir.");

        var query = _db.ClassSessions.Where(s => s.StudioId == studioId
            && s.Status == ClassSessionStatus.Scheduled && s.StartAt >= from && s.StartAt < to && s.StartAt > nowLocal);
        if (request.ClassTypeId is { } ct)
            query = query.Where(s => s.ClassTypeId == ct);

        var rows = await query
            .OrderBy(s => s.StartAt)
            .Select(s => new SessionRow(
                s.Id, s.ClassTypeId, s.ClassType!.Name, s.InstructorProfile!.DisplayName, s.StartAt, s.EndAt,
                s.Capacity, s.BookedCount, s.SessionKind,
                _db.ClassBookings.Where(b => b.ClassSessionId == s.Id && b.StudentProfileId == profileId && b.Status == ClassBookingStatus.Booked)
                    .Select(b => (Guid?)b.Id).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        // Paket uygunluğunu (kullanılabilir hak dahil) öğrenci başına tek seferde hesapla; seans başına uygula.
        var today = DateOnly.FromDateTime(nowLocal);
        var elig = await BuildStudentEligibilityAsync(studioId, profileId, today, cancellationToken);

        return rows.Select(r =>
        {
            // Paket kapısı yalnız Regular seansta; Makeup/Trial/Special → Eligible (paket gerekmez).
            var (status, name, available) = r.SessionKind == ClassSessionKind.Regular
                ? elig.Evaluate(r.ClassTypeId)
                : (PackageEligibilityStatus.Eligible, (string?)null, 0);
            return new StudentSessionDto(
                r.Id, r.ClassTypeName, r.InstructorName, r.StartAt, r.EndAt, r.Capacity, r.BookedCount, r.SessionKind,
                r.BookingId, r.BookingId != null,
                status, EligibilityMessage(status), name, available);
        }).ToList();
    }

    private static string? EligibilityMessage(PackageEligibilityStatus s) => s switch
    {
        PackageEligibilityStatus.NoPackageAtAll => "Yeni paket almanız gerekiyor.",
        PackageEligibilityStatus.NoBalance => "Paket hakkınız bitmiş.",
        PackageEligibilityStatus.WrongClassType => "Paketiniz bu ders için geçerli değil.",
        _ => null,
    };

    /// <summary>Öğrencinin paketlerini + kullanılabilir haklarını tek seferde yükleyip class-type bazında değerlendirir.</summary>
    private async Task<StudentEligibilitySnapshot> BuildStudentEligibilityAsync(
        Guid studioId, Guid profileId, DateOnly today, CancellationToken ct)
    {
        var pkgs = await _db.StudentPackages
            .Where(p => p.StudioId == studioId && p.StudentProfileId == profileId
                        && p.Status == StudentPackageStatus.Active
                        && (p.ValidUntil == null || p.ValidUntil >= today))
            .Select(p => new EligPkg(p.Id, p.Name, p.CurrentCreditBalance, p.AllowAllClassTypesSnapshot, p.ValidUntil, p.CreatedAt))
            .ToListAsync(ct);

        var allowed = new Dictionary<Guid, HashSet<Guid>>();
        var restricted = pkgs.Where(p => !p.AllowAll).Select(p => p.Id).ToList();
        if (restricted.Count > 0)
        {
            var arows = await _db.StudentPackageAllowedClassTypes
                .Where(a => a.StudioId == studioId && restricted.Contains(a.StudentPackageId))
                .Select(a => new { a.StudentPackageId, a.ClassTypeId }).ToListAsync(ct);
            foreach (var g in arows.GroupBy(a => a.StudentPackageId))
                allowed[g.Key] = g.Select(x => x.ClassTypeId).ToHashSet();
        }

        var pkgIds = pkgs.Select(p => p.Id).ToList();
        var reservedList = pkgIds.Count == 0 ? new List<Guid>() : await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.StudentProfileId == profileId
                        && b.Status == ClassBookingStatus.Booked
                        && b.ExpectedStudentPackageId != null && pkgIds.Contains(b.ExpectedStudentPackageId.Value)
                        && !_db.StudentPackageTransactions.Any(t =>
                               t.SourceClassBookingId == b.Id && t.Type == StudentPackageTransactionType.Used))
            .Select(b => b.ExpectedStudentPackageId!.Value).ToListAsync(ct);
        var reserved = reservedList.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        return new StudentEligibilitySnapshot(pkgs, allowed, reserved);
    }

    private sealed record SessionRow(
        Guid Id, Guid ClassTypeId, string ClassTypeName, string InstructorName, DateTime StartAt, DateTime EndAt,
        int Capacity, int BookedCount, ClassSessionKind SessionKind, Guid? BookingId);

    private sealed record EligPkg(Guid Id, string Name, int Balance, bool AllowAll, DateOnly? ValidUntil, DateTime CreatedAt);

    private sealed class StudentEligibilitySnapshot
    {
        private readonly IReadOnlyList<EligPkg> _pkgs;
        private readonly Dictionary<Guid, HashSet<Guid>> _allowed;
        private readonly Dictionary<Guid, int> _reserved;

        public StudentEligibilitySnapshot(IReadOnlyList<EligPkg> pkgs, Dictionary<Guid, HashSet<Guid>> allowed, Dictionary<Guid, int> reserved)
        {
            _pkgs = pkgs;
            _allowed = allowed;
            _reserved = reserved;
        }

        private bool Covers(EligPkg p, Guid classTypeId) =>
            p.AllowAll || (_allowed.TryGetValue(p.Id, out var set) && set.Contains(classTypeId));

        public (PackageEligibilityStatus Status, string? PackageName, int Available) Evaluate(Guid classTypeId)
        {
            if (_pkgs.Count == 0)
                return (PackageEligibilityStatus.NoPackageAtAll, null, 0);

            var covering = _pkgs.Where(p => Covers(p, classTypeId)).ToList();
            if (covering.Count == 0)
                return (PackageEligibilityStatus.WrongClassType, null, 0);

            var best = covering
                .Select(p => new { p.Name, p.ValidUntil, p.CreatedAt, Available = p.Balance - (_reserved.TryGetValue(p.Id, out var c) ? c : 0) })
                .Where(p => p.Available > 0)
                .OrderBy(p => p.ValidUntil == null).ThenBy(p => p.ValidUntil).ThenBy(p => p.CreatedAt)
                .FirstOrDefault();

            return best is null
                ? (PackageEligibilityStatus.NoBalance, null, 0)
                : (PackageEligibilityStatus.Eligible, best.Name, best.Available);
        }
    }
}
