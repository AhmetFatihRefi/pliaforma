using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Instructors;

/// <summary>Bir hocanın bu stüdyodaki ders ücretini günceller (Owner/StudioAdmin).</summary>
public sealed record UpdateInstructorRateCommand(
    Guid InstructorProfileId,
    decimal Rate,
    RateType RateType) : ICommand<Unit>, IStudioWriteRequest;

public sealed class UpdateInstructorRateValidator : AbstractValidator<UpdateInstructorRateCommand>
{
    public UpdateInstructorRateValidator()
    {
        RuleFor(x => x.InstructorProfileId).NotEmpty();
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateInstructorRateHandler : ICommandHandler<UpdateInstructorRateCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateInstructorRateHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateInstructorRateCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var profile = await _db.InstructorProfiles
            .FirstOrDefaultAsync(p => p.Id == request.InstructorProfileId && p.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Hoca profili", request.InstructorProfileId);

        profile.Rate = request.Rate;
        profile.RateType = request.RateType;
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
