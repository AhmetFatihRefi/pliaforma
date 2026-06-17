using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>Bir hocaya haftalık müsaitlik penceresi ekler (aynı gün çakışan pencere oluşturulamaz).</summary>
public sealed record CreateAvailabilityCommand(
    Guid InstructorProfileId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime) : ICommand<InstructorAvailabilityDto>, IStudioWriteRequest;

public sealed class CreateAvailabilityValidator : AbstractValidator<CreateAvailabilityCommand>
{
    public CreateAvailabilityValidator()
    {
        RuleFor(x => x.InstructorProfileId).NotEmpty();
        RuleFor(x => x).Must(x => x.StartTime < x.EndTime)
            .WithMessage("Başlangıç saati bitiş saatinden önce olmalı.");
    }
}

public sealed class CreateAvailabilityHandler : ICommandHandler<CreateAvailabilityCommand, InstructorAvailabilityDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateAvailabilityHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<InstructorAvailabilityDto> Handle(CreateAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, request.InstructorProfileId, cancellationToken);

        var overlap = await _db.InstructorAvailabilities.AnyAsync(
            a => a.StudioId == studioId
                 && a.InstructorProfileId == request.InstructorProfileId
                 && a.DayOfWeek == request.DayOfWeek
                 && a.IsActive
                 && a.StartTime < request.EndTime && request.StartTime < a.EndTime,
            cancellationToken);
        if (overlap)
            throw new ConflictException("Bu hoca için aynı günde çakışan bir müsaitlik var.");

        var entity = new InstructorAvailability
        {
            StudioId = studioId,
            InstructorProfileId = request.InstructorProfileId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsActive = true,
        };

        _db.InstructorAvailabilities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return InstructorAvailabilityDto.From(entity);
    }
}
