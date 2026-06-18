using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

// ---- E-posta doğrula (anon, token ile) ----
public sealed record VerifyEmailCommand(string Token) : ICommand<Unit>;

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator() => RuleFor(x => x.Token).NotEmpty();
}

public sealed class VerifyEmailHandler : ICommandHandler<VerifyEmailCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly EmailTokenIssuer _tokens;
    private readonly IDateTime _clock;

    public VerifyEmailHandler(IAppDbContext db, EmailTokenIssuer tokens, IDateTime clock)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<Unit> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var token = await _tokens.ConsumeAsync(request.Token, AuthTokenPurpose.EmailVerification, cancellationToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken)
            ?? throw new BusinessRuleException("Bağlantı geçersiz veya süresi dolmuş.");

        if (user.EmailVerifiedAt is null)
            user.EmailVerifiedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ---- Doğrulama e-postasını tekrar gönder (Authorize; cooldown; verified ise no-op) ----
public sealed record ResendVerificationEmailCommand : ICommand<Unit>;

public sealed class ResendVerificationEmailHandler : ICommandHandler<ResendVerificationEmailCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly EmailTokenIssuer _tokens;
    private readonly IEmailSender _email;

    public ResendVerificationEmailHandler(IAppDbContext db, ICurrentUserService currentUser,
        EmailTokenIssuer tokens, IEmailSender email)
    {
        _db = db;
        _currentUser = currentUser;
        _tokens = tokens;
        _email = email;
    }

    public async Task<Unit> Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("Oturum geçersiz.");

        // Zaten doğrulanmış → generic 200, yeni token YOK.
        if (user.EmailVerifiedAt is not null)
            return Unit.Value;

        // Cooldown içinde → sessiz generic 200, yeni token üretilmez (abuse engeli).
        if (await _tokens.InCooldownAsync(userId, AuthTokenPurpose.EmailVerification, AuthTokenRules.ResendCooldown, cancellationToken))
            return Unit.Value;

        var rawToken = await _tokens.IssueAsync(
            userId, AuthTokenPurpose.EmailVerification, AuthTokenRules.EmailVerificationTtl, null, null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _email.SendEmailVerificationAsync(user.Email, user.FullName, rawToken, cancellationToken);
        return Unit.Value;
    }
}
