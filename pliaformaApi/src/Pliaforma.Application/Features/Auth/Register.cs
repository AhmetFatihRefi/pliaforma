using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Stüdyosuz, global bir Standard kullanıcı hesabı oluşturur. Bu hesap ileride bir
/// stüdyoya davet/başvuru ile hoca veya öğrenci olarak bağlanabilir. Üyeliği olmadığı
/// için token aktif stüdyo bağlamı taşımaz.
/// </summary>
public sealed record RegisterCommand(
    string FullName,
    string Email,
    string Password,
    string? Phone,
    RegistrationIntent Intent = RegistrationIntent.Unknown,
    string? Ip = null) : ICommand<AuthResult>;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.Phone).MaximumLength(32);
    }
}

public sealed class RegisterHandler : ICommandHandler<RegisterCommand, AuthResult>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly AuthSessionService _session;
    private readonly EmailTokenIssuer _tokens;
    private readonly IEmailSender _email;

    public RegisterHandler(IAppDbContext db, IPasswordHasher hasher, AuthSessionService session,
        EmailTokenIssuer tokens, IEmailSender email)
    {
        _db = db;
        _hasher = hasher;
        _session = session;
        _tokens = tokens;
        _email = email;
    }

    public async Task<AuthResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new ConflictException("Bu e-posta ile zaten bir hesap var.");

        var user = new User
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            GlobalRole = GlobalRole.Standard,
            RegistrationIntent = request.Intent,   // Sprint 17 Rev2: UX niyeti (öğrenci/hoca)
            // Sprint 16: yeni kayıt doğrulanmamış (EmailVerifiedAt=null).
        };
        _db.Users.Add(user);

        // E-posta doğrulama token'ı + gönderim (dev → loglanır; Disabled → no-op).
        var rawToken = await _tokens.IssueAsync(
            user.Id, AuthTokenPurpose.EmailVerification, AuthTokenRules.EmailVerificationTtl, request.Ip, null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _email.SendEmailVerificationAsync(user.Email, user.FullName, rawToken, cancellationToken);

        return await _session.IssueAsync(user, activeMembership: null, request.Ip, cancellationToken);
    }
}
