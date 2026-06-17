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

    public RegisterHandler(IAppDbContext db, IPasswordHasher hasher, AuthSessionService session)
    {
        _db = db;
        _hasher = hasher;
        _session = session;
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
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return await _session.IssueAsync(user, activeMembership: null, request.Ip, cancellationToken);
    }
}
