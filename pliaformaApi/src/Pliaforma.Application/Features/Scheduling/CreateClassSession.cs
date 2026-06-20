using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>
/// Tek bir ders seansı oluşturur (Owner/StudioAdmin — stüdyo yetkilisi hocaya ders atar). EndAt,
/// ClassType.DefaultDurationMinutes ile hesaplanır; Capacity verilmezse ClassType.MaxCapacity. HARD kontrol:
/// geçmiş zaman + hoca çakışması (Sprint 26: müsaitlik penceresi kontrolü YOK). StartAt stüdyo-yerel duvar saati.
/// </summary>
public sealed record CreateClassSessionCommand(
    Guid InstructorProfileId,
    Guid ClassTypeId,
    DateTime StartAt,
    int? Capacity,
    string? Notes,
    ClassSessionKind SessionKind = ClassSessionKind.Regular) : ICommand<ClassSessionDto>, IStudioWriteRequest;

public sealed class CreateClassSessionValidator : AbstractValidator<CreateClassSessionCommand>
{
    public CreateClassSessionValidator()
    {
        RuleFor(x => x.InstructorProfileId).NotEmpty();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(1).When(x => x.Capacity.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateClassSessionHandler : ICommandHandler<CreateClassSessionCommand, ClassSessionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public CreateClassSessionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ClassSessionDto> Handle(CreateClassSessionCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profile = await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, request.InstructorProfileId, cancellationToken);

        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.ClassTypeId && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.ClassTypeId);

        var startAt = DateTime.SpecifyKind(request.StartAt, DateTimeKind.Unspecified);
        var endAt = startAt.AddMinutes(classType.DefaultDurationMinutes);
        // Sprint 28A: ClassType.MaxCapacity dormant → verilmezse grup-sınırsız (9999); bireysellik line/akıştan gelir.
        var capacity = request.Capacity ?? 9999;
        if (capacity < 1) capacity = 1;

        await SessionRules.EnsureValidAsync(_db, _clock, studioId, profile.Id, startAt, endAt, excludeId: null, cancellationToken);

        var entity = new ClassSession
        {
            StudioId = studioId,
            InstructorProfileId = profile.Id,
            ClassTypeId = classType.Id,
            StartAt = startAt,
            EndAt = endAt,
            Capacity = capacity,
            SessionKind = request.SessionKind,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = userId,
        };

        _db.ClassSessions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return ClassSessionDto.From(entity, profile.DisplayName, classType.Name);
    }
}
