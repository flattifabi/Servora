using Servora.Application.Common;

namespace Servora.Tests.Unit.Portal;

/// <summary>Adjustable clock for portal tests.</summary>
public sealed class PortalTestClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
}
