using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Closures;

/// <summary>
/// Toplu kapanış/erteleme: verilen tarih aralığındaki planlı seanslara seçilen aksiyonu uygular
/// (iptal / iptal+kredi / ofsetle erteleme). RECURRING DEĞİLDİR — yalnızca o anda mevcut seanslar
/// üzerinde tek seferlik toplu operasyon. Owner/StudioAdmin işlemidir (controller'da kısıtlı).
/// </summary>
public sealed record CreateStudioClosureCommand(
    string Title,
    string? Reason,
    DateTime StartAt,
    DateTime EndAt,
    StudioClosureActionType ActionType,
    int? OffsetDays,
    Guid? AffectedInstructorProfileId,
    Guid? AffectedClassTypeId) : ICommand<CreateClosureResultDto>, IStudioWriteRequest;

public sealed class CreateStudioClosureValidator : AbstractValidator<CreateStudioClosureCommand>
{
    public CreateStudioClosureValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x).Must(x => x.StartAt < x.EndAt).WithMessage("Başlangıç bitişten önce olmalı.");
        RuleFor(x => x.OffsetDays)
            .Must(o => o is { } v && v != 0)
            .When(x => x.ActionType == StudioClosureActionType.RescheduleByOffset)
            .WithMessage("Erteleme için 0 olmayan gün ofseti gerekli.");
    }
}

public sealed class CreateStudioClosureHandler : ICommandHandler<CreateStudioClosureCommand, CreateClosureResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly SessionCancellationService _cancellation;

    public CreateStudioClosureHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, SessionCancellationService cancellation)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _cancellation = cancellation;
    }

    public async Task<CreateClosureResultDto> Handle(CreateStudioClosureCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var startWall = DateTime.SpecifyKind(request.StartAt, DateTimeKind.Unspecified);
        var endWall = DateTime.SpecifyKind(request.EndAt, DateTimeKind.Unspecified);

        var query = _db.ClassSessions.Where(s =>
            s.StudioId == studioId && s.Status == ClassSessionStatus.Scheduled
            && s.StartAt >= startWall && s.StartAt <= endWall);
        if (request.AffectedInstructorProfileId is { } instr)
            query = query.Where(s => s.InstructorProfileId == instr);
        if (request.AffectedClassTypeId is { } ctype)
            query = query.Where(s => s.ClassTypeId == ctype);

        var sessions = await query.OrderBy(s => s.StartAt).ToListAsync(cancellationToken);
        var batchIds = sessions.Select(s => s.Id).ToHashSet();

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);
        var outcomes = new List<ClosureSessionOutcomeDto>(sessions.Count);
        int cancelled = 0, credited = 0, rescheduled = 0, skipped = 0;

        foreach (var session in sessions)
        {
            switch (request.ActionType)
            {
                case StudioClosureActionType.CancelSessions:
                case StudioClosureActionType.CancelAndGrantMakeupCredit:
                {
                    var grant = request.ActionType == StudioClosureActionType.CancelAndGrantMakeupCredit;
                    var reason = request.Title;
                    var (_, cr) = await _cancellation.CancelActiveBookingsAsync(
                        studioId, session.Id, userId, reason, grant, MakeupCreditReason.HolidayClosure, cancellationToken);
                    session.Status = ClassSessionStatus.Cancelled;
                    session.BookedCount = 0;
                    cancelled++;
                    credited += cr;
                    outcomes.Add(new ClosureSessionOutcomeDto(session.Id, session.StartAt, null,
                        grant ? "cancelled+credited" : "cancelled", null));
                    break;
                }

                case StudioClosureActionType.RescheduleByOffset:
                {
                    var offset = request.OffsetDays!.Value;
                    var newStart = session.StartAt.AddDays(offset);
                    var newEnd = session.EndAt.AddDays(offset);

                    var skipReason = await ResolveRescheduleSkipAsync(studioId, session, newStart, newEnd, batchIds, nowLocal, cancellationToken);
                    if (skipReason is not null)
                    {
                        skipped++;
                        outcomes.Add(new ClosureSessionOutcomeDto(session.Id, session.StartAt, newStart, "skipped", skipReason));
                        break;
                    }

                    session.StartAt = newStart;
                    session.EndAt = newEnd;
                    session.RescheduledAt = _clock.UtcNow;
                    session.RescheduleReason = request.Title;
                    rescheduled++;
                    outcomes.Add(new ClosureSessionOutcomeDto(session.Id, newStart.AddDays(-offset), newStart, "rescheduled", null));
                    break;
                }
            }
        }

        var closure = new StudioClosure
        {
            StudioId = studioId,
            Title = request.Title.Trim(),
            Reason = request.Reason?.Trim(),
            StartAt = startWall,
            EndAt = endWall,
            ActionType = request.ActionType,
            OffsetDays = request.OffsetDays,
            AffectedInstructorProfileId = request.AffectedInstructorProfileId,
            AffectedClassTypeId = request.AffectedClassTypeId,
            AffectedCount = sessions.Count,
            CancelledCount = cancelled,
            CreditedCount = credited,
            RescheduledCount = rescheduled,
            SkippedCount = skipped,
            CreatedByUserId = userId,
        };
        _db.StudioClosures.Add(closure);

        await _db.SaveChangesAsync(cancellationToken);
        return new CreateClosureResultDto(StudioClosureDto.From(closure), outcomes);
    }

    /// <summary>Erteleme için engel sebebi (past/conflict) ya da null (uygun).
    /// Sprint 26: müsaitlik (availability) kontrolü KALDIRILDI — yalnız gerçek çakışma + geçmiş.</summary>
    private async Task<string?> ResolveRescheduleSkipAsync(
        Guid studioId, ClassSession session, DateTime newStart, DateTime newEnd,
        HashSet<Guid> batchIds, DateTime nowLocal, CancellationToken ct)
    {
        if (newStart < nowLocal)
            return "past";

        // Çakışma: BATCH dışındaki Scheduled seanslarla (aynı ofset batch'i birlikte kaydığından kendi
        // aralarında bozulmaz; batch dışındakilerle çakışma gerçek çakışmadır).
        var conflict = await _db.ClassSessions.AnyAsync(
            s => s.StudioId == studioId && s.InstructorProfileId == session.InstructorProfileId
                 && s.Status == ClassSessionStatus.Scheduled
                 && !batchIds.Contains(s.Id)
                 && s.StartAt < newEnd && newStart < s.EndAt, ct);
        if (conflict)
            return "conflict";

        return null;
    }
}
