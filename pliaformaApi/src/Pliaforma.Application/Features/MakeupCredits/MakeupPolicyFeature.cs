using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Features.MakeupCredits;

public sealed record MakeupPolicyDto(
    int MonthlyManualCreditLimit,
    int DefaultCreditValidityDays,
    bool AllowInstructorManualGrant,
    bool AllowMakeupBookingWithoutCreditByAdmin)
{
    public static MakeupPolicyDto From(StudioMakeupPolicy p) =>
        new(p.MonthlyManualCreditLimit, p.DefaultCreditValidityDays, p.AllowInstructorManualGrant, p.AllowMakeupBookingWithoutCreditByAdmin);
}

// ---- Politikayı getir (yoksa varsayılan) ----
public sealed record GetMakeupPolicyQuery : IQuery<MakeupPolicyDto>;

public sealed class GetMakeupPolicyHandler : IQueryHandler<GetMakeupPolicyQuery, MakeupPolicyDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly MakeupCreditService _credits;

    public GetMakeupPolicyHandler(ICurrentUserService currentUser, MakeupCreditService credits)
    {
        _currentUser = currentUser;
        _credits = credits;
    }

    public async Task<MakeupPolicyDto> Handle(GetMakeupPolicyQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var policy = await _credits.GetPolicyAsync(studioId, cancellationToken);
        return MakeupPolicyDto.From(policy);
    }
}

// ---- Politikayı güncelle (Owner/StudioAdmin) ----
public sealed record UpdateMakeupPolicyCommand(
    int MonthlyManualCreditLimit,
    int DefaultCreditValidityDays,
    bool AllowInstructorManualGrant,
    bool AllowMakeupBookingWithoutCreditByAdmin) : ICommand<MakeupPolicyDto>, IStudioWriteRequest;

public sealed class UpdateMakeupPolicyValidator : AbstractValidator<UpdateMakeupPolicyCommand>
{
    public UpdateMakeupPolicyValidator()
    {
        RuleFor(x => x.MonthlyManualCreditLimit).InclusiveBetween(0, 100);
        RuleFor(x => x.DefaultCreditValidityDays).InclusiveBetween(0, 3650);
    }
}

public sealed class UpdateMakeupPolicyHandler : ICommandHandler<UpdateMakeupPolicyCommand, MakeupPolicyDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateMakeupPolicyHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MakeupPolicyDto> Handle(UpdateMakeupPolicyCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var policy = await _db.StudioMakeupPolicies.FirstOrDefaultAsync(p => p.StudioId == studioId, cancellationToken);
        if (policy is null)
        {
            policy = StudioMakeupPolicy.Default(studioId);
            _db.StudioMakeupPolicies.Add(policy);
        }

        policy.MonthlyManualCreditLimit = request.MonthlyManualCreditLimit;
        policy.DefaultCreditValidityDays = request.DefaultCreditValidityDays;
        policy.AllowInstructorManualGrant = request.AllowInstructorManualGrant;
        policy.AllowMakeupBookingWithoutCreditByAdmin = request.AllowMakeupBookingWithoutCreditByAdmin;

        await _db.SaveChangesAsync(cancellationToken);
        return MakeupPolicyDto.From(policy);
    }
}
