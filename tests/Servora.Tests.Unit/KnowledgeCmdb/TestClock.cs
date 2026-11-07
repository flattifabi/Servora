using Servora.Application.Common;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Fixed <see cref="IClock"/> for service tests.</summary>
public sealed class TestClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
}
