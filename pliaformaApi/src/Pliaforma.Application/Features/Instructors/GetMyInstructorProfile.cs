using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Instructors;

/// <summary>Mevcut kullanıcının aktif stüdyodaki hoca profili (kendi ders/müsaitlik yönetimi için).</summary>
public sealed record InstructorSelfDto(Guid InstructorProfileId, string DisplayName, decimal Rate, RateType RateType);

public sealed record GetMyInstructorProfileQuery : IQuery<InstructorSelfDto>;

public sealed class GetMyInstructorProfileHandler : IQueryHandler<GetMyInstructorProfileQuery, InstructorSelfDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyInstructorProfileHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<InstructorSelfDto> Handle(GetMyInstructorProfileQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var dto = await _db.InstructorProfiles
            .Where(p => p.StudioId == studioId && p.UserId == userId)
            .Select(p => new InstructorSelfDto(p.Id, p.DisplayName, p.Rate, p.RateType))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Bu stüdyoda hoca profiliniz bulunamadı.");

        return dto;
    }
}
