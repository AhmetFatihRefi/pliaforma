using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Platform;

/// <summary>Suspend/reactivate sonrası dönen küçük durum özeti.</summary>
public sealed record PlatformStudioStatusDto(
    Guid Id, StudioStatus Status, DateTime? SuspendedAt, string? SuspendedReason, DateTime? ReactivatedAt);

// ---- Askıya al ----
/// <summary>
/// Stüdyoyu askıya alır (PlatformAdmin). Status=Suspended; veri SİLİNMEZ. Stüdyo kullanıcıları
/// okuyabilir ama yazamaz (StudioStatusBehavior). ICommand'dır ama IStudioWriteRequest DEĞİLDİR:
/// stüdyo bağlamı gerektirmez ve kendi suspend kuralına takılmaz.
/// </summary>
public sealed record SuspendStudioCommand(Guid StudioId, string Reason) : ICommand<PlatformStudioStatusDto>;

public sealed class SuspendStudioValidator : AbstractValidator<SuspendStudioCommand>
{
    public SuspendStudioValidator()
    {
        RuleFor(x => x.StudioId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class SuspendStudioHandler : ICommandHandler<SuspendStudioCommand, PlatformStudioStatusDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public SuspendStudioHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PlatformStudioStatusDto> Handle(SuspendStudioCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var studio = await _db.Studios.FirstOrDefaultAsync(s => s.Id == request.StudioId, ct)
            ?? throw new NotFoundException("Stüdyo", request.StudioId);

        studio.Status = StudioStatus.Suspended;
        studio.SuspendedAt = _clock.UtcNow;
        studio.SuspendedReason = request.Reason.Trim();
        studio.UpdatedByPlatformAdminUserId = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return new PlatformStudioStatusDto(studio.Id, studio.Status, studio.SuspendedAt, studio.SuspendedReason, studio.ReactivatedAt);
    }
}

// ---- Yeniden aktifleştir ----
/// <summary>
/// Askıdaki stüdyoyu yeniden aktifleştirir (PlatformAdmin). Status=Active; yazma tekrar açılır.
/// SuspendedReason geçmiş olarak KORUNUR (ReactivatedAt ayrıca yazılır).
/// </summary>
public sealed record ReactivateStudioCommand(Guid StudioId) : ICommand<PlatformStudioStatusDto>;

public sealed class ReactivateStudioHandler : ICommandHandler<ReactivateStudioCommand, PlatformStudioStatusDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public ReactivateStudioHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PlatformStudioStatusDto> Handle(ReactivateStudioCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var studio = await _db.Studios.FirstOrDefaultAsync(s => s.Id == request.StudioId, ct)
            ?? throw new NotFoundException("Stüdyo", request.StudioId);

        studio.Status = StudioStatus.Active;
        studio.ReactivatedAt = _clock.UtcNow;
        studio.SuspendedAt = null; // aktif stüdyoda askı zamanı boş; gerekçe geçmiş olarak korunur.
        studio.UpdatedByPlatformAdminUserId = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return new PlatformStudioStatusDto(studio.Id, studio.Status, studio.SuspendedAt, studio.SuspendedReason, studio.ReactivatedAt);
    }
}
