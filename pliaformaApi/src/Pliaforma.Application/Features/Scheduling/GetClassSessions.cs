using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>
/// Ders seanslarını tarih aralığında listeler. Aralık verilmezse bugünden +30 gün; en fazla 90 gün.
/// Instructor yalnızca kendi seanslarını görür; Owner/StudioAdmin tümünü (opsiyonel filtrelerle).
/// </summary>
public sealed record GetClassSessionsQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? InstructorProfileId,
    Guid? ClassTypeId,
    ClassSessionStatus? Status) : IQuery<IReadOnlyList<ClassSessionDto>>;

public sealed class GetClassSessionsHandler : IQueryHandler<GetClassSessionsQuery, IReadOnlyList<ClassSessionDto>>
{
    private const int MaxRangeDays = 90;
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetClassSessionsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ClassSessionDto>> Handle(GetClassSessionsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var from = request.From ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var to = request.To ?? from.AddDays(30);
        if (to < from)
            throw new BusinessRuleException("Bitiş tarihi başlangıçtan önce olamaz.");
        if (to.DayNumber - from.DayNumber > MaxRangeDays)
            throw new BusinessRuleException($"Tarih aralığı en fazla {MaxRangeDays} gün olabilir.");

        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var query = _db.ClassSessions.Where(s => s.StudioId == studioId && s.StartAt >= fromDt && s.StartAt < toDt);

        var ownProfileId = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);
        if (ownProfileId is { } own)
            query = query.Where(s => s.InstructorProfileId == own); // Instructor → yalnızca kendi
        else if (request.InstructorProfileId is { } instr)
            query = query.Where(s => s.InstructorProfileId == instr);

        if (request.ClassTypeId is { } ct)
            query = query.Where(s => s.ClassTypeId == ct);
        if (request.Status is { } st)
            query = query.Where(s => s.Status == st);

        return await query
            .OrderBy(s => s.StartAt)
            .Select(s => new ClassSessionDto(
                s.Id, s.InstructorProfileId, s.InstructorProfile!.DisplayName,
                s.ClassTypeId, s.ClassType!.Name, s.StartAt, s.EndAt, s.Capacity, s.BookedCount, s.Status,
                s.SessionKind, s.Notes, s.CreatedByUserId, s.RescheduledAt))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Tek bir ders seansı (tenant + yetki).</summary>
public sealed record GetClassSessionQuery(Guid Id) : IQuery<ClassSessionDto>;

public sealed class GetClassSessionHandler : IQueryHandler<GetClassSessionQuery, ClassSessionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetClassSessionHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ClassSessionDto> Handle(GetClassSessionQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var dto = await _db.ClassSessions
            .Where(s => s.Id == request.Id && s.StudioId == studioId)
            .Select(s => new ClassSessionDto(
                s.Id, s.InstructorProfileId, s.InstructorProfile!.DisplayName,
                s.ClassTypeId, s.ClassType!.Name, s.StartAt, s.EndAt, s.Capacity, s.BookedCount, s.Status,
                s.SessionKind, s.Notes, s.CreatedByUserId, s.RescheduledAt))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.Id);

        // Instructor yalnızca kendi seansını görebilir.
        var ownProfileId = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);
        if (ownProfileId is { } own && dto.InstructorProfileId != own)
            throw new ForbiddenException("Bu ders seansı için yetkiniz yok.");

        return dto;
    }
}
