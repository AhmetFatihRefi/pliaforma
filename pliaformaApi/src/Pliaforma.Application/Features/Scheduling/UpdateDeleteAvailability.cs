using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

// ---- Güncelle ----
public sealed record UpdateAvailabilityCommand(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive) : ICommand<InstructorAvailabilityDto>, IStudioWriteRequest;

public sealed class UpdateAvailabilityValidator : AbstractValidator<UpdateAvailabilityCommand>
{
    public UpdateAvailabilityValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x).Must(x => x.StartTime < x.EndTime)
            .WithMessage("Başlangıç saati bitiş saatinden önce olmalı.");
    }
}

public sealed class UpdateAvailabilityHandler : ICommandHandler<UpdateAvailabilityCommand, InstructorAvailabilityDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateAvailabilityHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<InstructorAvailabilityDto> Handle(UpdateAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.InstructorAvailabilities
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Müsaitlik", request.Id);

        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, entity.InstructorProfileId, cancellationToken);

        var overlap = await _db.InstructorAvailabilities.AnyAsync(
            a => a.StudioId == studioId
                 && a.InstructorProfileId == entity.InstructorProfileId
                 && a.DayOfWeek == request.DayOfWeek
                 && a.IsActive
                 && a.Id != entity.Id
                 && a.StartTime < request.EndTime && request.StartTime < a.EndTime,
            cancellationToken);
        if (overlap)
            throw new ConflictException("Bu hoca için aynı günde çakışan bir müsaitlik var.");

        entity.DayOfWeek = request.DayOfWeek;
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return InstructorAvailabilityDto.From(entity);
    }
}

// ---- Sil (soft delete) ----
public sealed record DeleteAvailabilityCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class DeleteAvailabilityHandler : ICommandHandler<DeleteAvailabilityCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteAvailabilityHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.InstructorAvailabilities
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Müsaitlik", request.Id);

        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, entity.InstructorProfileId, cancellationToken);

        _db.InstructorAvailabilities.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
