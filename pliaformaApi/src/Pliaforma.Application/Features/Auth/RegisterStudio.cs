using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Application.Features.Platform;
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
    private readonly IDateTime _clock;
    private readonly ILogger<RegisterStudioHandler> _logger;
    private readonly EmailTokenIssuer _tokens;
    private readonly IEmailSender _email;

    public RegisterStudioHandler(IAppDbContext db, IPasswordHasher hasher, AuthSessionService session,
        IDateTime clock, ILogger<RegisterStudioHandler> logger, EmailTokenIssuer tokens, IEmailSender email)
    {
        _db = db;
        _hasher = hasher;
        _session = session;
        _clock = clock;
        _logger = logger;
        _tokens = tokens;
        _email = email;
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
            JoinCode = await GenerateUniqueJoinCodeAsync(cancellationToken),
            JoinCodeUpdatedAt = _clock.UtcNow,
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

        // Otomatik 14 günlük trial (varsayılan plan ile). Default plan yoksa: FAIL ETME — abonelik kaydı
        // oluşturulmaz (fail-open ile lisanslı kalır) ve uyarı loglanır.
        var defaultPlan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, cancellationToken);
        if (defaultPlan is not null)
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow);
            var trialEnd = today.AddDays(SubscriptionRules.TrialDays);
            _db.PlatformSubscriptions.Add(new PlatformSubscription
            {
                StudioId = studio.Id,
                SubscriptionPlanId = defaultPlan.Id,
                PlanName = defaultPlan.Name,
                Tier = defaultPlan.Tier,
                Status = SubscriptionStatus.Trialing,
                MonthlyPrice = defaultPlan.MonthlyPrice,
                Currency = defaultPlan.Currency,
                BillingPeriod = defaultPlan.BillingPeriod,
                IntroMonthlyPrice = defaultPlan.IntroMonthlyPrice,
                IntroPeriodsRemaining = defaultPlan.IntroPeriodCount,
                CurrentPeriodStart = today,
                CurrentPeriodEnd = trialEnd,
                TrialEndsAt = trialEnd,
            });
        }
        else
        {
            _logger.LogWarning("Varsayılan abonelik planı (IsDefault) bulunamadı — '{Studio}' için trial başlatılamadı (fail-open lisanslı).", studio.Name);
        }

        // Sprint 16 (Seçenek A): stüdyo akışı bozulmaz; owner EmailVerifiedAt=null kalır + doğrulama maili
        // gönderilir. Dashboard'da uyarı gösterilir; doğrulama bu sprintte ZORUNLU değil.
        var rawToken = await _tokens.IssueAsync(
            user.Id, AuthTokenPurpose.EmailVerification, AuthTokenRules.EmailVerificationTtl, request.Ip, null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _email.SendEmailVerificationAsync(user.Email, user.FullName, rawToken, cancellationToken);

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

    private async Task<string> GenerateUniqueJoinCodeAsync(CancellationToken ct)
    {
        string code;
        do { code = Common.Text.JoinCode.Generate(); }
        while (await _db.Studios.AnyAsync(s => s.JoinCode == code, ct));
        return code;
    }
}
