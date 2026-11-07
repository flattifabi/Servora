using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Domain.BusinessRules;

namespace Servora.Application.BusinessRules;

/// <summary>
/// Singleton cache of active business rules per table. Saving hooks run for every write, so rules are only loaded
/// from the database after <see cref="Invalidate"/> (called whenever a rule changes).
/// </summary>
public sealed class BusinessRuleCache
{
    /// <summary>The maximum number of active rules per table.</summary>
    public const int MaxRulesPerTable = 200;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ConcurrentDictionary<string, IReadOnlyList<CompiledBusinessRule>> _rules = new(StringComparer.OrdinalIgnoreCase);
    private long _generation;

    /// <summary>Gets the JSON options used to store actions.</summary>
    public static JsonSerializerOptions JsonOptions => s_json;

    /// <summary>Gets the active rules of a table.</summary>
    /// <param name="db">A database context used when the table is not cached.</param>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rules ordered by execution order.</returns>
    public async Task<IReadOnlyList<CompiledBusinessRule>> GetAsync(IAppDbContext db, string tableName, CancellationToken cancellationToken)
    {
        if (_rules.TryGetValue(tableName, out var cached))
        {
            return cached;
        }

        var generation = Interlocked.Read(ref _generation);
        var rules = await db.Set<BusinessRule>().AsNoTracking()
            .Where(r => r.TableName == tableName && r.IsActive)
            .OrderBy(r => r.Order).ThenBy(r => r.Name)
            .Take(MaxRulesPerTable)
            .ToListAsync(cancellationToken);
        var compiled = rules.Select(Compile).ToList();
        if (generation == Interlocked.Read(ref _generation))
        {
            _rules[tableName] = compiled;
        }

        return compiled;
    }

    /// <summary>Clears the cache.</summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _rules.Clear();
    }

    /// <summary>Parses the stored actions of a rule.</summary>
    /// <param name="json">The JSON array.</param>
    /// <returns>The actions (empty for invalid JSON).</returns>
    public static List<BusinessRuleAction> ParseActions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<BusinessRuleAction>>(json, s_json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static CompiledBusinessRule Compile(BusinessRule rule) => new(
        rule.Id,
        rule.Name,
        rule.Timing,
        rule.OnInsert,
        rule.OnUpdate,
        rule.OnDelete,
        ConditionSerializer.Deserialize(rule.ConditionJson),
        ParseActions(rule.ActionsJson));
}
