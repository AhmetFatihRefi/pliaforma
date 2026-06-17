using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Infrastructure.Services;

/// <summary>JWT claim'lerinden mevcut isteğin kimlik/tenant bağlamını çözer.</summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        TryGuid(Find(JwtRegisteredClaimNames.Sub) ?? Find(ClaimTypes.NameIdentifier));

    public GlobalRole? GlobalRole =>
        Enum.TryParse<GlobalRole>(Find(AppClaims.GlobalRole), out var role) ? role : null;

    public Guid? StudioId => TryGuid(Find(AppClaims.StudioId));

    public StudioRole? StudioRole =>
        Enum.TryParse<StudioRole>(Find(AppClaims.StudioRole), out var role) ? role : null;

    public Guid? MembershipId => TryGuid(Find(AppClaims.MembershipId));

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedException("Kimlik doğrulanmamış.");

    public Guid RequireStudioId() =>
        StudioId ?? throw new ForbiddenException("Aktif bir stüdyo bağlamı seçilmemiş.");

    private string? Find(string claimType) => Principal?.FindFirst(claimType)?.Value;

    private static Guid? TryGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : null;
}
