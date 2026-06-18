using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Members;

/// <summary>Stüdyo üyesi özeti (üyelik + varsa hoca profili bilgileri).</summary>
public sealed record MemberDto(
    Guid MembershipId,
    Guid UserId,
    string FullName,
    string Email,
    StudioRole Role,
    MembershipStatus Status,
    MembershipDirection Direction,
    Guid? InstructorProfileId,
    decimal? Rate,
    RateType? RateType);

/// <summary>Kullanıcının bekleyen (kendisine gelen) bir stüdyo davetinin özeti.</summary>
public sealed record PendingInvitationDto(
    Guid MembershipId,
    Guid StudioId,
    string StudioName,
    string StudioSlug,
    StudioRole Role,
    DateTime InvitedAt);
