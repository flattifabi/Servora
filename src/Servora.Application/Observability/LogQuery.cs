using Microsoft.Extensions.Logging;

namespace Servora.Application.Observability;

/// <summary>Filter for the in-memory log buffer. All criteria are optional and combined with AND.</summary>
public sealed record LogQuery
{
    /// <summary>The maximum number of entries a single query may return.</summary>
    public const int MaxTake = 10_000;

    private readonly int _take = 300;

    /// <summary>Gets the minimum level.</summary>
    public LogLevel? MinLevel { get; init; }

    /// <summary>Gets a category filter (case-insensitive substring).</summary>
    public string? Category { get; init; }

    /// <summary>Gets a free-text filter over message, exception and category (case-insensitive).</summary>
    public string? Search { get; init; }

    /// <summary>Gets a correlation id filter (exact match).</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Gets the sequence after which entries are returned (live tail).</summary>
    public long? AfterSequence { get; init; }

    /// <summary>Gets the earliest timestamp.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Gets the latest timestamp.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Gets the maximum number of entries, clamped to 1..<see cref="MaxTake"/>.</summary>
    public int Take { get => _take; init => _take = Math.Clamp(value, 1, MaxTake); }
}
