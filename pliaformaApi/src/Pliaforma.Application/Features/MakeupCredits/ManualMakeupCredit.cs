using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.MakeupCredits;

// ---- Manuel kredi ver (Sprint 28C-2: lineId ZORUNLU — kredi line'a yazılır) ----
public sealed record GrantMakeupCreditCommand(Guid StudentProfileId, Guid StudentContractLineId, int Amount, string? Notes)
    : ICommand<StudentMakeupCreditsDto>, IStudioWriteRequest;

public sealed class GrantMakeupCreditValidator : AbstractValidator<GrantMakeupCreditCommand>
{
    public GrantMakeupCreditValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty();
        RuleFor(x => x.StudentContractLineId).NotEmpty().WithMessage("Telafi kredisi bir sözleşme satırına (line) yazılmalı.");
        RuleFor(x => x.Amount).InclusiveBetween(1, 10);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class GrantMakeupCreditHandler : ICommandHandler<GrantMakeupCreditCommand, StudentMakeupCreditsDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly MakeupCreditService _credits;

    public GrantMakeupCreditHandler(IAppDbContext db, ICurrentUserService currentUser, MakeupCreditService credits)
    {
        _db = db;
        _currentUser = currentUser;
        _credits = credits;
    }

    public async Task<StudentMakeupCreditsDto> Handle(GrantMakeupCreditCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var role = _currentUser.StudioRole;

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        var policy = await _credits.GetPolicyAsync(studioId, cancellationToken);

        // Instructor manuel kredi yetkisi politikaya bağlı.
        if (role == StudioRole.Instructor && !policy.AllowInstructorManualGrant)
            throw new ForbiddenException("Politika gereği hocalar manuel telafi kredisi veremez.");

        await MakeupCreditAccess.EnsureCanManageAsync(_db, _currentUser, studioId, student, cancellationToken);

        // Sprint 28C-2: kredi bir LINE'a yazılır → line öğrenciye ait + aktif olmalı.
        var line = await _db.StudentContractLines
            .FirstOrDefaultAsync(l => l.Id == request.StudentContractLineId && l.StudioId == studioId
                                       && l.StudentContract!.StudentProfileId == student.Id, cancellationToken)
            ?? throw new NotFoundException("Sözleşme satırı", request.StudentContractLineId);
        if (line.Status != StudentContractLineStatus.Active)
            throw new BusinessRuleException("Pasif/iptal satıra telafi kredisi yazılamaz.");

        // Aylık manuel kredi limiti (yalnızca manuel grant'lere uygulanır; iptal/tatil kredileri hariç).
        if (policy.MonthlyManualCreditLimit > 0)
        {
            var grantedThisMonth = await _credits.ManualGrantedThisMonthAsync(studioId, student.Id, cancellationToken);
            if (grantedThisMonth + request.Amount > policy.MonthlyManualCreditLimit)
                throw new BusinessRuleException(
                    $"Aylık manuel kredi limiti ({policy.MonthlyManualCreditLimit}) aşılır. Bu ay verilen: {grantedThisMonth}.");
        }

        var reason = role == StudioRole.Instructor ? MakeupCreditReason.ManualByInstructor : MakeupCreditReason.ManualByOwner;
        _credits.Grant(studioId, student.Id, line.Id, request.Amount, reason, userId,
            expiresAt: _credits.ComputeExpiry(policy), notes: request.Notes);

        await _db.SaveChangesAsync(cancellationToken);

        return await SummaryBuilder.BuildAsync(_db, studioId, student, cancellationToken);
    }
}

// ---- Manuel kredi geri al ----
// Sprint 28C-2: revoke LINE-scoped (line bakiyesinden düşer).
public sealed record RevokeMakeupCreditCommand(Guid StudentProfileId, Guid StudentContractLineId, int Amount, string? Notes)
    : ICommand<StudentMakeupCreditsDto>, IStudioWriteRequest;

public sealed class RevokeMakeupCreditValidator : AbstractValidator<RevokeMakeupCreditCommand>
{
    public RevokeMakeupCreditValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty();
        RuleFor(x => x.StudentContractLineId).NotEmpty().WithMessage("Geri alma bir sözleşme satırından (line) yapılmalı.");
        RuleFor(x => x.Amount).InclusiveBetween(1, 10);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class RevokeMakeupCreditHandler : ICommandHandler<RevokeMakeupCreditCommand, StudentMakeupCreditsDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly MakeupCreditService _credits;
    private readonly IMakeupCreditLock _lock;

    public RevokeMakeupCreditHandler(IAppDbContext db, ICurrentUserService currentUser, MakeupCreditService credits, IMakeupCreditLock @lock)
    {
        _db = db;
        _currentUser = currentUser;
        _credits = credits;
        _lock = @lock;
    }

    public async Task<StudentMakeupCreditsDto> Handle(RevokeMakeupCreditCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        await MakeupCreditAccess.EnsureCanManageAsync(_db, _currentUser, studioId, student, cancellationToken);

        var line = await _db.StudentContractLines
            .FirstOrDefaultAsync(l => l.Id == request.StudentContractLineId && l.StudioId == studioId
                                       && l.StudentContract!.StudentProfileId == student.Id, cancellationToken)
            ?? throw new NotFoundException("Sözleşme satırı", request.StudentContractLineId);

        // Sprint 28C-2: LINE bakiyesini negatife düşürmemek için kilitle, sonra line bakiyesini oku.
        await _lock.LockStudentAsync(studioId, student.Id, cancellationToken);
        var balance = await _credits.GetLineBalanceAsync(studioId, line.Id, cancellationToken);
        if (request.Amount > balance)
            throw new BusinessRuleException($"Geri alınacak miktar satır bakiyesinden ({balance}) fazla olamaz.");

        _credits.Revoke(studioId, student.Id, line.Id, request.Amount, userId, MakeupCreditReason.Other, request.Notes);
        await _db.SaveChangesAsync(cancellationToken);

        return await SummaryBuilder.BuildAsync(_db, studioId, student, cancellationToken);
    }
}

internal static class SummaryBuilder
{
    public static async Task<StudentMakeupCreditsDto> BuildAsync(
        IAppDbContext db, Guid studioId, StudentProfile student, CancellationToken ct)
    {
        var (balance, summary, byLine, txs) = await MakeupCreditView.BuildLedgerAsync(db, studioId, student.Id, ct);
        return new StudentMakeupCreditsDto(student.Id, student.FullName, balance, summary, byLine, txs);
    }
}
