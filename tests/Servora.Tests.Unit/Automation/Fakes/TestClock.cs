using Servora.Application.Common;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary>Controllable <see cref="IClock"/> for automation tests.</summary>
internal sealed class TestClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Moves the clock forward.</summary>
    /// <param name="duration">The duration.</param>
    public void Advance(TimeSpan duration) => UtcNow += duration;
}
