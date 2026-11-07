namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary>Time provider whose clock is set by the test.</summary>
/// <param name="now">The initial time.</param>
/// <param name="localTimeZone">The local time zone (UTC by default).</param>
internal sealed class ManualTimeProvider(DateTimeOffset now, TimeZoneInfo? localTimeZone = null) : TimeProvider
{
    /// <summary>Gets or sets the current time.</summary>
    public DateTimeOffset Now { get; set; } = now;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => localTimeZone ?? TimeZoneInfo.Utc;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    /// <summary>Advances the clock.</summary>
    /// <param name="delta">The amount of time.</param>
    public void Advance(TimeSpan delta) => Now = Now.Add(delta);
}
