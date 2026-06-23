using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Platform;

/// <summary>
/// Sprint 31C-1 — PlatformAdmin güvenlik denetim görüntüleyicisi (salt-okunur, cross-tenant).
/// <b>Studio scoping UYGULANMAZ</b>: PlatformAdmin tüm stüdyoların kayıtlarını görür; current studio ile
/// filtrelenmez (studioId yalnız OPSİYONEL filtre). PII zaten yazımda maskeli (ActorEmailMasked) — viewer ek
/// işlem yapmaz. Export YOK (CSV → 31D). Studio adı UX için <see cref="IAppDbContext.Studios"/> ile çözülür.
/// </summary>
public sealed record GetSecurityAuditLogsQuery(
    DateTime? From,
    DateTime? To,
    SecurityAuditEventType? EventType,
    Guid? ActorUserId,
    Guid? StudioId,
    SecurityAuditOutcome? Outcome,
    int Page = 1,
    int PageSize = 20) : IQuery<SecurityAuditLogPageDto>;

public sealed record SecurityAuditLogRowDto(
    Guid Id,
    DateTime OccurredAt,
    string EventType,
    string Outcome,
    Guid? ActorUserId,
    string? ActorEmailMasked,
    Guid? StudioId,
    string? StudioName,
    string? TargetType,
    Guid? TargetId,
    string? Ip,
    string? UserAgent,
    string? Detail);

public sealed record SecurityAuditLogPageDto(
    IReadOnlyList<SecurityAuditLogRowDto> Items,
    int Page,
    int PageSize,
    int Total);

public sealed class GetSecurityAuditLogsHandler : IQueryHandler<GetSecurityAuditLogsQuery, SecurityAuditLogPageDto>
{
    private const int MaxPageSize = 100;
    private readonly IAppDbContext _db;

    public GetSecurityAuditLogsHandler(IAppDbContext db) => _db = db;

    public async Task<SecurityAuditLogPageDto> Handle(GetSecurityAuditLogsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize switch { < 1 => 20, > MaxPageSize => MaxPageSize, var n => n };

        // DİKKAT: studio scoping yok — PlatformAdmin global görünüm. Tüm filtreler opsiyonel.
        var query = _db.SecurityAuditLogs.AsNoTracking();

        if (request.From is { } from)
            query = query.Where(x => x.OccurredAt >= from);
        if (request.To is { } to)
            query = query.Where(x => x.OccurredAt <= to);
        if (request.EventType is { } et)
            query = query.Where(x => x.EventType == et);
        if (request.Outcome is { } oc)
            query = query.Where(x => x.Outcome == oc);
        if (request.ActorUserId is { } actor)
            query = query.Where(x => x.ActorUserId == actor);
        if (request.StudioId is { } sid)
            query = query.Where(x => x.StudioId == sid);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SecurityAuditLogRowDto(
                x.Id,
                x.OccurredAt,
                x.EventType.ToString(),
                x.Outcome.ToString(),
                x.ActorUserId,
                x.ActorEmailMasked,
                x.StudioId,
                // Studio adı UX için (cross-tenant; scoping değil). Stüdyo silinmiş/null ise null.
                x.StudioId == null ? null : _db.Studios.Where(s => s.Id == x.StudioId).Select(s => s.Name).FirstOrDefault(),
                x.TargetType,
                x.TargetId,
                x.Ip,
                x.UserAgent,
                x.Detail))
            .ToListAsync(ct);

        return new SecurityAuditLogPageDto(items, page, pageSize, total);
    }
}
