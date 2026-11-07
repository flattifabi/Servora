using Servora.Application.Common;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>Fixed, adjustable clock.</summary>
public sealed class TestClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
}
