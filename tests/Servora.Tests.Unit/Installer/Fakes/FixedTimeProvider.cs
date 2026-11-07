namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Time provider with a fixed wall clock in UTC (timers and timestamps stay real).</summary>
/// <param name="now">The fixed current time.</param>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
