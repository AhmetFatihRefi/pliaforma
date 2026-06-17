using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

/// <summary>Tek bir öğrenciyi getirir (tenant + yetki kontrolü). Sağlık notu yetkiye göre döner.</summary>
public sealed record GetStudentQuery(Guid Id) : IQuery<StudentDto>;

public sealed class GetStudentHandler : IQueryHandler<GetStudentQuery, StudentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStudentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StudentDto> Handle(GetStudentQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.Id);

        StudentAccess.EnsureCanManage(_currentUser, student);

        return StudentDto.From(student, includeHealth: StudentAccess.CanSeeHealth(_currentUser, student));
    }
}
