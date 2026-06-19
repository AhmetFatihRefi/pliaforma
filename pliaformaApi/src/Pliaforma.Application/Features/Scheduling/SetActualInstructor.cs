using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>
/// Bir seansın FİİLEN dersi veren hocasını set/temizler (Sprint 19). null → planlanan hoca verdi.
/// Dersi iptal ETMEZ, telafi DOĞURMAZ; yalnız atıf (rapor) + "hoca değişikliği" rozeti içindir.
/// Yalnız Owner/StudioAdmin (controller + handler doğrular). Instructor değiştiremez.
/// </summary>
public sealed record SetActualInstructorCommand(Guid SessionId, Guid? ActualInstructorProfileId)
    : ICommand<Unit>, IStudioWriteRequest;

public sealed class SetActualInstructorValidator : AbstractValidator<SetActualInstructorCommand>
{
    public SetActualInstructorValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}

public sealed class SetActualInstructorHandler : ICommandHandler<SetActualInstructorCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SetActualInstructorHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetActualInstructorCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        // Yalnız Owner/StudioAdmin fiilî hoca atayabilir.
        if (_currentUser.StudioRole is not (StudioRole.Owner or StudioRole.StudioAdmin))
            throw new ForbiddenException("Fiilî hoca değişikliği yalnızca Owner/StudioAdmin yetkisindedir.");

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.SessionId);

        if (session.Status == ClassSessionStatus.Cancelled)
            throw new ConflictException("İptal edilmiş seansta hoca değişikliği yapılamaz.");

        // Fiilî hoca verildiyse aynı stüdyoda geçerli bir hoca profili olmalı.
        if (request.ActualInstructorProfileId is { } actualId)
        {
            var exists = await _db.InstructorProfiles
                .AnyAsync(p => p.Id == actualId && p.StudioId == studioId, cancellationToken);
            if (!exists)
                throw new NotFoundException("Hoca profili", actualId);
        }

        // null veya planlanan hocaya eşitse → "değişiklik yok" (alan temizlenir).
        session.ActualInstructorProfileId =
            request.ActualInstructorProfileId == session.InstructorProfileId ? null : request.ActualInstructorProfileId;

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
