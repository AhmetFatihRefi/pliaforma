using Pliaforma.Application.Common.Interfaces;

namespace Pliaforma.Infrastructure.Services;

public sealed class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}
