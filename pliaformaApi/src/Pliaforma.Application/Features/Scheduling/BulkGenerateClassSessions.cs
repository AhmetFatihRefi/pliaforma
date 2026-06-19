using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>
/// Tek gün / tek aralık içinde ardışık ders seansları üretir (ClassType süresi + opsiyonel ara).
/// Bu RECURRING booking DEĞİLDİR. Geçmiş/çakışan slotlar atlanır; created+skipped döner.
/// Sprint 26: müsaitlik penceresi kontrolü YOK — yalnız gerçek çakışma + geçmiş atlanır.
/// </summary>
public sealed record BulkGenerateClassSessionsCommand(
    Guid InstructorProfileId,
    Guid ClassTypeId,
    DateOnly Date,
    TimeOnly RangeStart,
    TimeOnly RangeEnd,
    int? Capacity,
    int? BreakMinutes) : ICommand<BulkGenerateResultDto>, IStudioWriteRequest;

public sealed class BulkGenerateClassSessionsValidator : AbstractValidator<BulkGenerateClassSessionsCommand>
{
    public BulkGenerateClassSessionsValidator()
    {
        RuleFor(x => x.InstructorProfileId).NotEmpty();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x).Must(x => x.RangeStart < x.RangeEnd).WithMessage("Aralık başlangıcı bitişten önce olmalı.");
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(1).When(x => x.Capacity.HasValue);
        RuleFor(x => x.BreakMinutes).InclusiveBetween(0, 240).When(x => x.BreakMinutes.HasValue);
    }
}

public sealed class BulkGenerateClassSessionsHandler
    : ICommandHandler<BulkGenerateClassSessionsCommand, BulkGenerateResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public BulkGenerateClassSessionsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<BulkGenerateResultDto> Handle(BulkGenerateClassSessionsCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profile = await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, request.InstructorProfileId, cancellationToken);
        var classType = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.ClassTypeId && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.ClassTypeId);

        var duration = classType.DefaultDurationMinutes;
        var gap = request.BreakMinutes ?? 0;
        var capacity = request.Capacity ?? classType.MaxCapacity;
        if (capacity < 1) capacity = 1;

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);

        // O günkü mevcut Scheduled seanslar (çakışma için, in-memory).
        var dayStart = request.Date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var existing = await _db.ClassSessions
            .Where(s => s.StudioId == studioId && s.InstructorProfileId == profile.Id
                        && s.Status == ClassSessionStatus.Scheduled && s.StartAt < dayEnd && s.EndAt > dayStart)
            .Select(s => new { s.StartAt, s.EndAt })
            .ToListAsync(cancellationToken);

        var created = new List<ClassSession>();
        var skipped = new List<SkippedSlotDto>();

        var rangeStart = request.Date.ToDateTime(request.RangeStart);
        var rangeEnd = request.Date.ToDateTime(request.RangeEnd);

        for (var cursor = rangeStart; cursor.AddMinutes(duration) <= rangeEnd; cursor = cursor.AddMinutes(duration + gap))
        {
            var start = DateTime.SpecifyKind(cursor, DateTimeKind.Unspecified);
            var end = start.AddMinutes(duration);

            if (start < nowLocal) { skipped.Add(new SkippedSlotDto(start, "past")); continue; }
            if (existing.Any(x => x.StartAt < end && start < x.EndAt)
                || created.Any(c => c.StartAt < end && start < c.EndAt))
            {
                skipped.Add(new SkippedSlotDto(start, "conflict"));
                continue;
            }

            created.Add(new ClassSession
            {
                StudioId = studioId,
                InstructorProfileId = profile.Id,
                ClassTypeId = classType.Id,
                StartAt = start,
                EndAt = end,
                Capacity = capacity,
                CreatedByUserId = userId,
            });
        }

        if (created.Count > 0)
        {
            _db.ClassSessions.AddRange(created);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var createdDtos = created
            .Select(s => ClassSessionDto.From(s, profile.DisplayName, classType.Name))
            .ToList();

        return new BulkGenerateResultDto(createdDtos, skipped);
    }
}
