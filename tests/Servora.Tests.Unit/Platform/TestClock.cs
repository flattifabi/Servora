using Servora.Application.Common;

namespace Servora.Tests.Unit.Platform;

/// <summary>Controllable clock.</summary>
public sealed class TestClock : IClock
{
    /// <summary>Gets or sets the current time (defaults to the real time).</summary>
    public DateTimeOffset? Now { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => Now ?? DateTimeOffset.UtcNow;
}
