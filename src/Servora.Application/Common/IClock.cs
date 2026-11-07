namespace Servora.Application.Common;

/// <summary>Abstraction over the system clock so time-dependent logic (SLAs, schedules, expiry) is testable.</summary>
public interface IClock
{
    /// <summary>Gets the current UTC time.</summary>
    DateTimeOffset UtcNow { get; }
}
