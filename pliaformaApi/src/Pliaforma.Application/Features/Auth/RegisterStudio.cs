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
/// Yeni bir stüdyo + sahibi hesabı oluşturur. User (Standard) + Studio + Owner
/// membership (Active) yaratır ve aktif stüdyo bağlamıyla token döner.
/// </summary>
public sealed record RegisterStudioCommand(
    string StudioName,
    string OwnerFullName,
    string Email,
    string Password,
    string? Phone,
    string? Ip = null) : ICommand<AuthResult>;

public sealed class RegisterStudioValidator : AbstractValidator<RegisterStudioCommand>
{
    public RegisterStudioValidator()
    {
        RuleFor(x => x.StudioName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.OwnerFullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.Phone).MaximumLength(32);
    }
}

public sealed class RegisterStudioHandler : ICommandHandler<RegisterStudioCommand, AuthResult>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly AuthSessionService _session;

    public RegisterStudioHandler(IAppDbContext db, IPasswordHasher hasher, AuthSessionService session)
    {
        _db = db;
        _hasher = hasher;
        _session = session;
    }

    public async Task<AuthResult> Handle(RegisterStudioCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new ConflictException("Bu e-posta ile zaten bir hesap var.");

        var user = new User
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.OwnerFullName.Trim(),
            Phone = request.Phone?.Trim(),
            GlobalRole = GlobalRole.Standard,
        };

        var studio = new Studio
        {
            Name = request.StudioName.Trim(),
            Slug = await GenerateUniqueSlugAsync(request.StudioName, cancellationToken),
            OwnerUserId = user.Id,
        };

        var membership = new StudioMembership
        {
            StudioId = studio.Id,
            UserId = user.Id,
            Role = StudioRole.Owner,
            Status = MembershipStatus.Active,
            Direction = MembershipDirection.RequestedByUser,
            AcceptedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        _db.Studios.Add(studio);
        _db.StudioMemberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return await _session.IssueAsync(user, membership, request.Ip, cancellationToken);
    }

    private async Task<string> GenerateUniqueSlugAsync(string studioName, CancellationToken ct)
    {
        var baseSlug = Slug.From(studioName);
        var slug = baseSlug;
        var suffix = 1;

        while (await _db.Studios.AnyAsync(s => s.Slug == slug, ct))
            slug = $"{baseSlug}-{++suffix}";

        return slug;
    }
}
