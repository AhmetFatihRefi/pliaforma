using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Equipments;
using Pliaforma.Application.Features.Studios;
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
    ClassSessionKind SessionKind = ClassSessionKind.Regular,
    // Sprint 42: yalnız geçmiş-zaman engelini aşar (manuel backfill). Default false; true sadece UI onayıyla gelir.
    bool AllowPastSession = false) : ICommand<ClassSessionDto>, IStudioWriteRequest;

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
    private readonly EquipmentCapacityGuard _equipmentGuard;
    private readonly ISecurityAuditLogger _audit;

    public CreateClassSessionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, EquipmentCapacityGuard equipmentGuard, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _equipmentGuard = equipmentGuard;
        _audit = audit;
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

        await SessionRules.EnsureValidAsync(_db, _clock, studioId, profile.Id, startAt, endAt, excludeId: null, cancellationToken, allowPast: request.AllowPastSession);

        // Sprint 37: seans tamamen stüdyo çalışma saatleri içinde olmalı (kapalı gün/saat dışı/kapanış aşımı → 409).
        var workingHours = await StudioWorkingHoursService.LoadAsync(_db, studioId, cancellationToken);
        StudioWorkingHoursService.EnsureWithin(workingHours, startAt, endAt);

        // Sprint 32B: ekipman-bağlı ders tipinde kapasite guard (sınırsız yasak, ≤toplam, ≤available; havuz kilidi).
        await _equipmentGuard.EnsureCanReserveAsync(studioId, classType, startAt, endAt, capacity, excludeSessionId: null, pendingOverlapCapacity: 0, cancellationToken);

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

        // Sprint 42: geçmiş tarihli (backfill) ders oluşturma denetim kaydı (best-effort; PII yok).
        if (request.AllowPastSession)
            await _audit.LogAsync(SecurityAuditEventType.PastSessionCreated, SecurityAuditOutcome.Success,
                actorUserId: userId, studioId: studioId,
                targetType: "ClassSession", targetId: entity.Id,
                detail: $"startAt={startAt:yyyy-MM-ddTHH:mm}", cancellationToken: cancellationToken);

        return ClassSessionDto.From(entity, profile.DisplayName, classType.Name);
    }
}
