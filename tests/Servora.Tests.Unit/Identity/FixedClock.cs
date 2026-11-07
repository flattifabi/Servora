using Servora.Application.Common;

namespace Servora.Tests.Unit.Identity;

/// <summary>Clock returning a settable time.</summary>
public sealed class FixedClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
}
