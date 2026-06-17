using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>
/// Staff: bir öğrencinin aylık katılım özeti. Owner/StudioAdmin tüm öğrenciler; Instructor yalnızca
/// erişebildiği öğrenciler (kendi oluşturduğu ya da kendi seansında rezervasyonu olan).
/// </summary>
public sealed record GetStudentAttendanceSummaryQuery(Guid StudentProfileId, string? Month) : IQuery<AttendanceSummaryDto>;

public sealed class GetStudentAttendanceSummaryHandler : IQueryHandler<GetStudentAttendanceSummaryQuery, AttendanceSummaryDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetStudentAttendanceSummaryHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AttendanceSummaryDto> Handle(GetStudentAttendanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        await MakeupCreditAccess.EnsureCanManageAsync(_db, _currentUser, studioId, student, cancellationToken);

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);
        return await AttendanceSummaries.ComputeAsync(_db, studioId, student.Id, request.Month, nowLocal, cancellationToken);
    }
}
