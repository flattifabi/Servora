using Servora.Domain.Integration;

namespace Servora.Application.Integration.Imports;

/// <summary>An import run with its counters.</summary>
/// <param name="Id">The run.</param>
/// <param name="ImportSetId">The import set.</param>
/// <param name="Status">The status.</param>
/// <param name="SourceDescription">The source.</param>
/// <param name="StartedAt">The start time.</param>
/// <param name="CompletedAt">The completion time.</param>
/// <param name="TotalRows">Staged rows.</param>
/// <param name="Inserted">Inserted records.</param>
/// <param name="Updated">Updated records.</param>
/// <param name="Skipped">Skipped rows.</param>
/// <param name="Errors">Failed rows.</param>
/// <param name="StartedBy">The starting actor.</param>
/// <param name="Error">The run error.</param>
public sealed record ImportRunItem(
    Guid Id,
    Guid ImportSetId,
    ImportRunStatus Status,
    string? SourceDescription,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int TotalRows,
    int Inserted,
    int Updated,
    int Skipped,
    int Errors,
    string? StartedBy,
    string? Error);
