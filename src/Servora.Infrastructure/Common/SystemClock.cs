using Servora.Application.Common;

namespace Servora.Infrastructure.Common;

/// <summary>System clock implementation of <see cref="IClock"/>.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
