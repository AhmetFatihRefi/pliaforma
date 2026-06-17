using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

// ---- Pasifleştir ----
public sealed record DeactivateStudentCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class DeactivateStudentHandler : ICommandHandler<DeactivateStudentCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeactivateStudentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeactivateStudentCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.Id);

        StudentAccess.EnsureCanManage(_currentUser, student);

        student.Status = StudentStatus.Inactive;
        student.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ---- Yeniden aktifleştir ----
public sealed record ReactivateStudentCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class ReactivateStudentHandler : ICommandHandler<ReactivateStudentCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReactivateStudentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ReactivateStudentCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.Id);

        StudentAccess.EnsureCanManage(_currentUser, student);

        student.IsActive = true;
        // Kullanıcıya bağlıysa Active, değilse misafir (Guest) durumuna döner.
        student.Status = student.UserId is not null ? StudentStatus.Active : StudentStatus.Guest;
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
