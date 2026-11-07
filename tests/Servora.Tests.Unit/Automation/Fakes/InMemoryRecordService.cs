using System.Collections.Concurrent;
using System.Globalization;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Tables;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary>In-memory <see cref="IRecordService"/> (equality filters only) that records every write.</summary>
internal sealed class InMemoryRecordService : IRecordService
{
    private readonly ConcurrentDictionary<(string Table, Guid Id), Dictionary<string, object?>> _records = new();

    /// <summary>Gets the update calls.</summary>
    public ConcurrentQueue<(string Table, Guid Id, IReadOnlyDictionary<string, object?> Values)> Updates { get; } = new();

    /// <summary>Gets the create calls.</summary>
    public ConcurrentQueue<(string Table, IReadOnlyDictionary<string, object?> Values)> Creates { get; } = new();

    /// <summary>Adds a record directly.</summary>
    /// <param name="table">The table.</param>
    /// <param name="values">The values.</param>
    /// <returns>The record identifier.</returns>
    public Guid Seed(string table, IReadOnlyDictionary<string, object?> values)
    {
        var id = Guid.CreateVersion7();
        _records[(table.ToLowerInvariant(), id)] = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase) { ["Id"] = id };
        return id;
    }

    /// <summary>Gets all records of a table.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The records.</returns>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> All(string table) =>
        _records.Where(r => r.Key.Table == table.ToLowerInvariant()).Select(r => (IReadOnlyDictionary<string, object?>)r.Value).ToList();

    /// <inheritdoc />
    public Task<Result<PagedResult<IReadOnlyDictionary<string, object?>>>> QueryAsync(string tableName, RecordQuery query, CancellationToken cancellationToken = default)
    {
        var matches = All(tableName).Where(r => Matches(r, query.Condition)).ToList();
        var page = matches.Skip(query.Page.Skip).Take(query.Page.PageSize).ToList();
        return Task.FromResult(Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Success(
            new PagedResult<IReadOnlyDictionary<string, object?>>(page, matches.Count, query.Page.Page, query.Page.PageSize)));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyDictionary<string, object?>>> GetAsync(string tableName, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_records.TryGetValue((tableName.ToLowerInvariant(), id), out var record)
            ? Result<IReadOnlyDictionary<string, object?>>.Success(new Dictionary<string, object?>(record, StringComparer.OrdinalIgnoreCase))
            : Result<IReadOnlyDictionary<string, object?>>.NotFound());

    /// <inheritdoc />
    public Task<Result<Guid>> CreateAsync(string tableName, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        Creates.Enqueue((tableName, values));
        return Task.FromResult(Result<Guid>.Success(Seed(tableName, values)));
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(string tableName, Guid id, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        if (!_records.TryGetValue((tableName.ToLowerInvariant(), id), out var record))
        {
            return Task.FromResult(Result.NotFound());
        }

        Updates.Enqueue((tableName, id, values));
        foreach (var pair in values)
        {
            record[pair.Key] = pair.Value;
        }

        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(string tableName, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_records.TryRemove((tableName.ToLowerInvariant(), id), out _) ? Result.Success() : Result.NotFound());

    private static bool Matches(IReadOnlyDictionary<string, object?> record, ConditionGroup? condition) =>
        condition is null || condition.Rules.All(rule => rule.Operator != ConditionOperator.Equals
            || string.Equals(Convert.ToString(record.GetValueOrDefault(rule.Field), CultureInfo.InvariantCulture), rule.Value, StringComparison.OrdinalIgnoreCase));
}
