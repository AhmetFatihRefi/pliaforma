using Microsoft.Extensions.Configuration;
using Pliaforma.Application.Common.Interfaces;

namespace Pliaforma.Infrastructure.Services;

/// <summary>Sprint 46: Pilot bayrağını appsettings/env'den okur (<c>Pilot:SimplePeopleMode</c>, default false).</summary>
public sealed class PilotModeInfo : IPilotModeInfo
{
    public bool SimplePeopleModeEnabled { get; }

    public PilotModeInfo(IConfiguration config)
    {
        SimplePeopleModeEnabled = config.GetValue<bool?>("Pilot:SimplePeopleMode") ?? false;
    }
}
