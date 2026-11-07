using Servora.Application.Common;

namespace Servora.Tests.Unit.Itsm;

/// <summary>Controllable <see cref="IClock"/> for tests.</summary>
internal sealed class FixedClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
}
