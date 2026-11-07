using Servora.Application.Common;

namespace Servora.Tests.Unit.Observability;

/// <summary>Settable clock for tests.</summary>
internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = now;

    /// <summary>Advances the clock.</summary>
    /// <param name="by">The time span.</param>
    public void Advance(TimeSpan by) => UtcNow += by;
}
