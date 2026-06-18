using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Aktif stüdyoya kayıtlı bir kullanıcıyı StudioAdmin veya Instructor olarak davet eder. Yalnız **Pending
/// üyelik (InvitedByStudio)** oluşturur — **profil oluşturulmaz** (Sprint 17). Profil (Instructor/Student)
/// yalnızca davet edilen kullanıcı daveti kabul ettiğinde (AcceptMembership → EnsureRoleProfileAsync)
/// oluşur; böylece kabulden önce davet edilen kişi aktif hoca olarak görünmez. Davet edilen kullanıcıya
/// bir InApp bildirim (MembershipInvite) üretilir (dedup'lı). **Yalnızca Owner, StudioAdmin rolü ile davet
/// edebilir** (StudioAdmin yalnızca Instructor davet eder).
/// </summary>
public sealed record InviteMemberCommand(
    string Email,
    StudioRole Role,
    string? DisplayName,
    decimal? Rate,
    RateType? RateType) : ICommand<MemberDto>, IStudioWriteRequest;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        // Rev3: invite-only onboarding → stüdyo Öğrenci, Hoca veya StudioAdmin davet edebilir (Owner yok; o register'la gelir).
        RuleFor(x => x.Role)
            .Must(r => r is StudioRole.StudioAdmin or StudioRole.Instructor or StudioRole.Student)
            .WithMessage("Rol yalnızca StudioAdmin, Instructor veya Student olabilir.");
        RuleFor(x => x.DisplayName).MaximumLength(150);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0).When(x => x.Rate.HasValue);
    }
}

public sealed class InviteMemberHandler : ICommandHandler<InviteMemberCommand, MemberDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly INotificationChannelSender _notifications;
    private readonly ILogger<InviteMemberHandler> _logger;

    public InviteMemberHandler(
        IAppDbContext db,
        ICurrentUserService currentUser,
        IDateTime clock,
        INotificationChannelSender notifications,
        ILogger<InviteMemberHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<MemberDto> Handle(InviteMemberCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        // Owner koruması: yalnızca Owner, StudioAdmin atayabilir.
        if (request.Role == StudioRole.StudioAdmin && _currentUser.StudioRole != StudioRole.Owner)
            throw new ForbiddenException("Yalnızca stüdyo sahibi StudioAdmin davet edebilir.");

        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        // Sprint 17: hesabı olmayan kişi davet edilemez (e-posta davet linki bu sürümde yok) — mesaj net.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken)
            ?? throw new ConflictException("Bu e-posta ile kayıtlı kullanıcı bulunamadı. Davet edilecek kişi önce hesap oluşturmalı.");

        var existing = await _db.StudioMemberships
            .AnyAsync(m => m.StudioId == studioId && m.UserId == user.Id, cancellationToken);
        if (existing)
            throw new ConflictException("Bu kullanıcı zaten bu stüdyonun üyesi (veya bekleyen daveti var).");

        // Sprint 17: profil İNVITE anında oluşturulmaz. Yalnız Pending üyelik. Profil kabulde (AcceptMembership)
        // EnsureRoleProfileAsync ile oluşur → kabulden önce hoca aktif (liste/availability/schedule) görünmez.
        var membership = new StudioMembership
        {
            StudioId = studioId,
            UserId = user.Id,
            Role = request.Role,
            Status = MembershipStatus.Pending,
            Direction = MembershipDirection.InvitedByStudio,
        };
        _db.StudioMemberships.Add(membership);

        // Davet bildirimi (InApp). DeduplicationKey = membership Id → aynı davet için ikinci bildirim üretilmez
        // (Notification'da (StudioId, DeduplicationKey) benzersiz index). Stüdyo adı body için gerekir.
        var studioName = await _db.Studios.Where(s => s.Id == studioId).Select(s => s.Name).FirstAsync(cancellationToken);
        await CreateInviteNotificationAsync(studioId, user.Id, membership.Id, studioName, request.Role, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // Profil yok → InstructorProfileId/Rate/RateType null (kabul sonrası stüdyo ücreti belirler).
        return new MemberDto(membership.Id, user.Id, user.FullName, user.Email, membership.Role,
            membership.Status, membership.Direction, null, null, null);
    }

    /// <summary>Davet edilen kullanıcıya MembershipInvite bildirimi oluşturur ve InApp kanalına teslim eder
    /// (idempotent: aynı membership için zaten varsa tekrar üretmez). Teslim hatası daveti bozmaz.</summary>
    private async Task CreateInviteNotificationAsync(
        Guid studioId, Guid recipientUserId, Guid membershipId, string studioName, StudioRole role, CancellationToken ct)
    {
        var dedupKey = $"membership-invite:{membershipId}";
        var already = await _db.Notifications
            .AnyAsync(n => n.StudioId == studioId && n.DeduplicationKey == dedupKey, ct);
        if (already) return;

        var notification = new Notification
        {
            StudioId = studioId,
            RecipientUserId = recipientUserId,
            Type = NotificationType.MembershipInvite,
            Channel = NotificationChannel.InApp,
            Title = "Stüdyo daveti",
            Body = $"{studioName} sizi {RoleLabel(role)} olarak davet etti.",
            DeduplicationKey = dedupKey,
            ScheduledAtUtc = _clock.UtcNow,
        };

        try
        {
            var ok = await _notifications.SendAsync(notification, ct);
            notification.Status = ok ? NotificationStatus.Sent : NotificationStatus.Failed;
            if (ok) notification.SentAtUtc = _clock.UtcNow;
            else notification.FailureReason = "Kanal teslim edemedi (bu sürümde yalnızca InApp/Log aktif).";
        }
        catch (Exception ex)
        {
            notification.Status = NotificationStatus.Failed;
            notification.FailureReason = ex.Message;
            _logger.LogWarning(ex, "Davet bildirimi teslimi başarısız: {DedupKey}", dedupKey);
        }

        _db.Notifications.Add(notification);
    }

    private static string RoleLabel(StudioRole role) => role switch
    {
        StudioRole.Instructor => "hoca",
        StudioRole.StudioAdmin => "yönetici",
        StudioRole.Student => "öğrenci",
        _ => role.ToString(),
    };
}
