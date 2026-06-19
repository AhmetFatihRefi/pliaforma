using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>
/// Sprint 20: Ders HOCA NEDENİYLE işlenemedi (ve başka hoca da işlemedi) → seans <b>Cancelled</b> + aktif
/// rezervasyon sahiplerine <b>ekstra telafi</b> (<see cref="MakeupCreditReason.InstructorNotDelivered"/>,
/// sözleşme <c>MakeupAllowance</c>'tan BAĞIMSIZ, seans başına idempotent). Domain ayrımı:
/// <list type="bullet">
/// <item>Ertelendi (yeni tarihte yapılacak) → <c>Reschedule</c> (telafi YOK).</item>
/// <item>Başka hoca verdi → <c>SetActualInstructor</c> (telafi YOK).</item>
/// <item>Gerçekten işlenmedi → bu komut (+1 telafi).</item>
/// </list>
/// Yalnız Owner/StudioAdmin.
/// </summary>
public sealed record MarkSessionNotDeliveredCommand(Guid SessionId, string? Reason = null)
    : ICommand<Unit>, IStudioWriteRequest;

public sealed class MarkSessionNotDeliveredHandler : ICommandHandler<MarkSessionNotDeliveredCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly SessionCancellationService _cancellation;

    public MarkSessionNotDeliveredHandler(IAppDbContext db, ICurrentUserService currentUser, SessionCancellationService cancellation)
    {
        _db = db;
        _currentUser = currentUser;
        _cancellation = cancellation;
    }

    public async Task<Unit> Handle(MarkSessionNotDeliveredCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        // Yalnız Owner/StudioAdmin "işlenmedi" işaretleyebilir (telafi üretir).
        if (_currentUser.StudioRole is not (StudioRole.Owner or StudioRole.StudioAdmin))
            throw new ForbiddenException("Dersi 'işlenmedi' işaretleme yetkisi yalnızca Owner/StudioAdmin'dedir.");

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.SessionId);

        if (session.Status == ClassSessionStatus.Cancelled)
            throw new ConflictException("Bu seans zaten iptal edilmiş.");
        if (session.Status == ClassSessionStatus.Completed)
            throw new ConflictException("Tamamlanmış (verilmiş) ders 'işlenmedi' olarak işaretlenemez.");

        session.Status = ClassSessionStatus.Cancelled;

        // Aktif rezervasyonlar iptal + öğrencilere telafi (InstructorNotDelivered). Tüketilmiş kredi varsa iade.
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Ders hoca nedeniyle işlenemedi." : request.Reason.Trim();
        await _cancellation.CancelActiveBookingsAsync(
            studioId, session.Id, userId, reason, grantMakeupCredit: true,
            MakeupCreditReason.InstructorNotDelivered, cancellationToken);
        session.BookedCount = 0;

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
