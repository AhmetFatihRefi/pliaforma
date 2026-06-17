namespace Pliaforma.Application.Common.Security;

/// <summary>Authorization policy adları.</summary>
public static class AuthPolicies
{
    /// <summary>Yalnızca global_role == PlatformAdmin olan kullanıcılar. Platform (cross-tenant) uçları için.</summary>
    public const string PlatformAdmin = "PlatformAdmin";
}
