using Servora.Application.Common;

namespace Servora.Tests.Unit.Api.Fakes;

/// <summary>Controllable <see cref="IClock"/>.</summary>
internal sealed class TestClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
}
