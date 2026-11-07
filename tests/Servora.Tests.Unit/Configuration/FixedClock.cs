using Servora.Application.Common;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Settable <see cref="IClock"/>.</summary>
internal sealed class FixedClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
}
