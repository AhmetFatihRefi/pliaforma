using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Instructors;

/// <summary>
/// Bir hocayı bir pilates grubuna (ClassType) atar; opsiyonel ücret override. Hem hoca profili
/// hem grup aktif stüdyoya ait olmalıdır (tenant izolasyonu). Owner/StudioAdmin.
/// </summary>
public sealed record AssignClassTypeCommand(
    Guid InstructorProfileId,
    Guid ClassTypeId,
    decimal? RateOverride) : ICommand<InstructorAssignmentDto>, IStudioWriteRequest;

public sealed class AssignClassTypeValidator : AbstractValidator<AssignClassTypeCommand>
{
    public AssignClassTypeValidator()
    {
        RuleFor(x => x.InstructorProfileId).NotEmpty();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.RateOverride).GreaterThanOrEqualTo(0).When(x => x.RateOverride.HasValue);
    }
}

public sealed class AssignClassTypeHandler : ICommandHandler<AssignClassTypeCommand, InstructorAssignmentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AssignClassTypeHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<InstructorAssignmentDto> Handle(AssignClassTypeCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var profile = await _db.InstructorProfiles
            .FirstOrDefaultAsync(p => p.Id == request.InstructorProfileId && p.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Hoca profili", request.InstructorProfileId);

        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.ClassTypeId && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.ClassTypeId);

        var exists = await _db.InstructorClassTypes.AnyAsync(
            x => x.InstructorProfileId == profile.Id && x.ClassTypeId == classType.Id, cancellationToken);
        if (exists)
            throw new ConflictException("Bu hoca zaten bu gruba atanmış.");

        var entity = new InstructorClassType
        {
            StudioId = studioId,
            InstructorProfileId = profile.Id,
            ClassTypeId = classType.Id,
            RateOverride = request.RateOverride,
        };

        _db.InstructorClassTypes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new InstructorAssignmentDto(entity.Id, classType.Id, classType.Name, entity.RateOverride);
    }
}
