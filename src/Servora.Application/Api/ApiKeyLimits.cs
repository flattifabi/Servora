namespace Servora.Application.Api;

/// <summary>Limits and defaults of API keys.</summary>
public static class ApiKeyLimits
{
    /// <summary>The default rate limit (requests per minute).</summary>
    public const int DefaultRateLimitPerMinute = 120;

    /// <summary>The highest configurable rate limit.</summary>
    public const int MaxRateLimitPerMinute = 10_000;

    /// <summary>The longest allowed lifetime of a key.</summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(3 * 365);

    /// <summary>Keys expiring within this period count as "expiring soon".</summary>
    public static readonly TimeSpan ExpiringSoonWindow = TimeSpan.FromDays(14);
}
