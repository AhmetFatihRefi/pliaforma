using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

// ---- DTO'lar ----
/// <summary>Zincirde kayacak tek ders (eski/yeni saat + opsiyonel gerçek çakışma bilgisi).</summary>
public sealed record ChainShiftItemDto(
    Guid SessionId, string ClassTypeName,
    DateTime OldStartAt, DateTime OldEndAt, DateTime NewStartAt, DateTime NewEndAt,
    string? ConflictReason);   // null | "past" | "instructor" | "student"  (Sprint 26: availability YOK)

/// <summary>Zincirleme kaydırma önizlemesi: kayacak dersler + uygulanabilir mi.</summary>
public sealed record ChainShiftPreviewDto(IReadOnlyList<ChainShiftItemDto> Affected, bool CanApply);

// ---- Ortak planlayıcı ----
internal static class ChainShiftPlanner
{
    internal sealed record Member(ClassSession Session, DateTime NewStart, DateTime NewEnd);

    /// <summary>
    /// Aynı hocanın ardışık Scheduled derslerinden zinciri kurar: taşınan ders <paramref name="newStart"/>'a gider;
    /// kaymış bitiş bir sonrakinin ORİJİNAL başlangıcını geçiyorsa o ders de aynı <c>delta</c> kadar kayar; boşlukta
    /// zincir durur. İleri-yönlü (delta &gt; 0) kaskad; geri taşımada zincir yalnız taşınan derstir (çakışma ayrı yakalanır).
    /// </summary>
    public static async Task<List<Member>> BuildAsync(IAppDbContext db, Guid studioId, ClassSession moved, DateTime newStart, CancellationToken ct)
    {
        var delta = newStart - moved.StartAt;

        var sessions = await db.ClassSessions
            .Where(x => x.StudioId == studioId && x.InstructorProfileId == moved.InstructorProfileId
                        && x.Status == ClassSessionStatus.Scheduled)
            .OrderBy(x => x.StartAt).ThenBy(x => x.Id)
            .ToListAsync(ct);

        var chain = new List<Member>();
        var started = false;
        var prevNewEnd = DateTime.MinValue;
        foreach (var x in sessions)
        {
            if (x.Id == moved.Id)
            {
                var ne = moved.EndAt + delta;
                chain.Add(new Member(x, newStart, ne));
                prevNewEnd = ne;
                started = true;
                continue;
            }
            if (!started) continue;                 // taşınan dersten ÖNCEKİ dersler zincire girmez
            if (x.StartAt < prevNewEnd)              // örtüşme → aynı delta ile kay
            {
                var ns = x.StartAt + delta;
                var ne = x.EndAt + delta;
                chain.Add(new Member(x, ns, ne));
                prevNewEnd = ne;
            }
            else break;                             // boşluk → zincir durur
        }
        return chain;
    }

    /// <summary>Zinciri değerlendirir: her üye için yalnız GERÇEK çakışma (past/instructor/student). Mutasyon yok.
    /// Sprint 26: müsaitlik (availability) değerlendirmesi KALDIRILDI.</summary>
    public static async Task<List<ChainShiftItemDto>> EvaluateAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, List<Member> chain, CancellationToken ct)
    {
        var chainIds = chain.Select(m => m.Session.Id).ToHashSet();
        var nowLocal = await SessionRules.StudioNowAsync(db, clock, studioId, ct);

        // Toplu yükleme: ders tipi adları + zincirdeki seansların booked öğrencileri.
        var sessionIds = chainIds.ToList();
        var classTypeNames = await db.ClassSessions
            .Where(s => sessionIds.Contains(s.Id))
            .Select(s => new { s.Id, Name = s.ClassType!.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var bookingsByStudent = (await db.ClassBookings
            .Where(b => b.StudioId == studioId && sessionIds.Contains(b.ClassSessionId) && b.Status == ClassBookingStatus.Booked)
            .Select(b => new { b.ClassSessionId, b.StudentProfileId })
            .ToListAsync(ct))
            .GroupBy(b => b.ClassSessionId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentProfileId).ToList());

        var instructorId = chain[0].Session.InstructorProfileId;

        var items = new List<ChainShiftItemDto>(chain.Count);
        foreach (var m in chain)
        {
            string? reason = null;
            if (m.NewStart < nowLocal)
                reason = "past";
            else if (await db.ClassSessions.AnyAsync(s => s.StudioId == studioId
                         && s.InstructorProfileId == instructorId && s.Status == ClassSessionStatus.Scheduled
                         && !chainIds.Contains(s.Id) && s.StartAt < m.NewEnd && m.NewStart < s.EndAt, ct))
                reason = "instructor";
            else
            {
                var studentIds = bookingsByStudent.TryGetValue(m.Session.Id, out var ids) ? ids : new List<Guid>();
                if (studentIds.Count > 0 && await SessionRules.StudentConflictAsync(db, studioId, studentIds, m.NewStart, m.NewEnd, chainIds, ct))
                    reason = "student";
            }

            items.Add(new ChainShiftItemDto(
                m.Session.Id, classTypeNames.TryGetValue(m.Session.Id, out var n) ? n : "",
                m.Session.StartAt, m.Session.EndAt, m.NewStart, m.NewEnd, reason));
        }
        return items;
    }

    public static async Task<ClassSession> LoadMovableAsync(IAppDbContext db, ICurrentUserService user, Guid studioId, Guid sessionId, CancellationToken ct)
    {
        // Zincirleme kaydırma yalnız Owner/StudioAdmin (zincir birden çok dersi etkiler).
        if (user.StudioRole is not (StudioRole.Owner or StudioRole.StudioAdmin))
            throw new ForbiddenException("Zincirleme kaydırma yetkisi yalnızca Owner/StudioAdmin'dedir.");

        var session = await db.ClassSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders seansı", sessionId);
        if (session.Status != ClassSessionStatus.Scheduled)
            throw new ConflictException("Yalnızca planlı (Scheduled) seanslar kaydırılabilir.");
        return session;
    }
}

// ---- Önizleme (read-only) ----
public sealed record PreviewChainShiftQuery(Guid SessionId, DateTime NewStartAt) : IQuery<ChainShiftPreviewDto>;

public sealed class PreviewChainShiftHandler : IQueryHandler<PreviewChainShiftQuery, ChainShiftPreviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public PreviewChainShiftHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ChainShiftPreviewDto> Handle(PreviewChainShiftQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var session = await ChainShiftPlanner.LoadMovableAsync(_db, _currentUser, studioId, request.SessionId, cancellationToken);

        var newStart = DateTime.SpecifyKind(request.NewStartAt, DateTimeKind.Unspecified);
        var chain = await ChainShiftPlanner.BuildAsync(_db, studioId, session, newStart, cancellationToken);
        var items = await ChainShiftPlanner.EvaluateAsync(_db, _clock, studioId, chain, cancellationToken);
        return new ChainShiftPreviewDto(items, items.All(i => i.ConflictReason is null));
    }
}

// ---- Uygula (atomik; telafi YOK) ----
public sealed record ChainShiftSessionCommand(Guid SessionId, DateTime NewStartAt, string? Reason = null)
    : ICommand<ChainShiftPreviewDto>, IStudioWriteRequest;

public sealed class ChainShiftSessionHandler : ICommandHandler<ChainShiftSessionCommand, ChainShiftPreviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public ChainShiftSessionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ChainShiftPreviewDto> Handle(ChainShiftSessionCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var session = await ChainShiftPlanner.LoadMovableAsync(_db, _currentUser, studioId, request.SessionId, cancellationToken);

        var newStart = DateTime.SpecifyKind(request.NewStartAt, DateTimeKind.Unspecified);
        var chain = await ChainShiftPlanner.BuildAsync(_db, studioId, session, newStart, cancellationToken);
        var items = await ChainShiftPlanner.EvaluateAsync(_db, _clock, studioId, chain, cancellationToken);

        // Atomik: herhangi bir HARD çakışma varsa hiçbir ders kaymaz.
        var conflict = items.FirstOrDefault(i => i.ConflictReason is not null);
        if (conflict is not null)
            throw new ConflictException(conflict.ConflictReason switch
            {
                "past" => "Zincirde geçmiş bir zamana taşınan ders var.",
                "instructor" => "Zincir, hocanın başka bir dersiyle çakışıyor.",
                "student" => "Zincir, bir öğrencinin başka dersiyle çakışıyor.",
                _ => "Zincir uygulanamıyor (çakışma).",
            });

        // Uygula — telafi ÜRETİLMEZ (KeepBookings; ders yapılacak, sadece taşınıyor).
        var reason = request.Reason?.Trim();
        var now = _clock.UtcNow;
        foreach (var m in chain)
        {
            m.Session.StartAt = m.NewStart;
            m.Session.EndAt = m.NewEnd;
            m.Session.RescheduledAt = now;
            m.Session.RescheduleReason = reason;
        }
        await _db.SaveChangesAsync(cancellationToken);

        return new ChainShiftPreviewDto(items, true);
    }
}
