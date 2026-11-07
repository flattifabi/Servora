namespace Servora.Application.Integration.DataSources;

/// <summary>In-process sliding-window rate limiter per data source (requests per minute). Register as singleton.</summary>
public sealed class DataSourceRateLimiter
{
    private static readonly TimeSpan s_window = TimeSpan.FromMinutes(1);
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _windows = [];
    private readonly Lock _lock = new();

    /// <summary>Tries to acquire a permit.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <param name="perMinute">The limit; 0 or less means unlimited.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when the request may proceed.</returns>
    public bool TryAcquire(Guid dataSourceId, int perMinute, DateTimeOffset now)
    {
        if (perMinute <= 0)
        {
            return true;
        }

        lock (_lock)
        {
            if (!_windows.TryGetValue(dataSourceId, out var window))
            {
                window = new Queue<DateTimeOffset>();
                _windows[dataSourceId] = window;
            }

            while (window.Count > 0 && now - window.Peek() >= s_window)
            {
                window.Dequeue();
            }

            if (window.Count >= perMinute)
            {
                return false;
            }

            window.Enqueue(now);
            return true;
        }
    }
}
