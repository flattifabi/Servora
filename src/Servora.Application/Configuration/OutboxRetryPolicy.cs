namespace Servora.Application.Configuration;

/// <summary>Exponential back-off schedule of the mail outbox: 1 min, 5 min, 15 min, 1 h, then every 4 h.</summary>
public static class OutboxRetryPolicy
{
    private static readonly TimeSpan[] s_delays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4),
    ];

    /// <summary>Gets the delay before the next attempt.</summary>
    /// <param name="attemptsMade">The number of attempts made so far (at least 1).</param>
    /// <returns>The delay.</returns>
    public static TimeSpan GetDelay(int attemptsMade) => s_delays[Math.Clamp(attemptsMade, 1, s_delays.Length) - 1];

    /// <summary>Computes the time of the next attempt after a failure.</summary>
    /// <param name="attemptsMade">The number of attempts made so far, including the failed one.</param>
    /// <param name="maxAttempts">The maximum number of attempts.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The time of the next attempt, or <see langword="null"/> when no attempts are left.</returns>
    public static DateTimeOffset? GetNextAttempt(int attemptsMade, int maxAttempts, DateTimeOffset now) =>
        attemptsMade >= Math.Max(1, maxAttempts) ? null : now + GetDelay(attemptsMade);
}
