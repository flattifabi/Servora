using System.Diagnostics;
using System.Linq.Expressions;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Tables;
using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>
/// Executes report specifications. Typed tables (core and module entities) are queried directly with bounded,
/// provider-neutral projections (only the group and value columns); custom tables go through <see cref="IRecordService"/>.
/// Grouping and aggregation happen in memory. Table access is enforced for the current actor; only fields exposed by the
/// table registry (never <c>[NotExposed]</c> properties) can be used.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="records">The generic record service (custom tables, access probe).</param>
/// <param name="evaluator">The condition evaluator.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
public sealed class ReportEngine(
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    IRecordService records,
    IConditionEvaluator evaluator,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>The maximum number of records evaluated for typed tables.</summary>
    public const int MaxTypedRows = 50_000;

    /// <summary>The page size used for custom tables.</summary>
    public const int CustomPageSize = 500;

    /// <summary>The maximum number of pages read for custom tables.</summary>
    public const int MaxCustomPages = 20;

    private static readonly System.Reflection.MethodInfo s_toExpression = typeof(IConditionEvaluator).GetMethod(nameof(IConditionEvaluator.ToExpression))!;

    /// <summary>Checks whether a field can be used for grouping.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when groupable.</returns>
    public static bool IsGroupable(FieldDefinition field) =>
        field.Type is FieldType.String or FieldType.Choice or FieldType.Boolean or FieldType.Reference or FieldType.DateTime
            or FieldType.Date or FieldType.Integer or FieldType.Email or FieldType.Url;

    /// <summary>Checks whether a field can be aggregated numerically.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> for integer and decimal fields.</returns>
    public static bool IsNumeric(FieldDefinition field) => field.Type is FieldType.Integer or FieldType.Decimal;

    /// <summary>Validates a specification against a table definition.</summary>
    /// <param name="spec">The specification.</param>
    /// <param name="table">The table.</param>
    /// <returns>An error message, or <see langword="null"/> when valid.</returns>
    public static string? Validate(ReportSpec spec, TableDefinition table)
    {
        if (spec.GroupBy is { Length: > 0 } groupBy && (table.FindField(groupBy) is not { } groupField || !IsGroupable(groupField)))
        {
            return $"'{groupBy}' cannot be used for grouping.";
        }

        if (spec.Aggregate != ReportAggregate.Count)
        {
            if (string.IsNullOrEmpty(spec.AggregateField))
            {
                return "Choose the numeric field to aggregate.";
            }

            if (table.FindField(spec.AggregateField) is not { } valueField || !IsNumeric(valueField))
            {
                return $"'{spec.AggregateField}' is not a numeric field.";
            }
        }

        if (spec.MaxGroups is < 2 or > ReportSpec.MaxGroupLimit)
        {
            return $"The number of groups must be between 2 and {ReportSpec.MaxGroupLimit}.";
        }

        return spec.ChartType != ReportChartType.Metric && string.IsNullOrEmpty(spec.GroupBy) && spec.ChartType != ReportChartType.Table
            ? "Choose a field to group by, or use the 'Metric' visualization for a single value."
            : null;
    }

    /// <summary>Checks whether the current actor may read a table for reporting.</summary>
    /// <param name="table">The table.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanReadAsync(TableDefinition table, CancellationToken cancellationToken)
    {
        if (await TableAccessPolicy.CanReadAsync(currentUser, table.Name, cancellationToken))
        {
            return true;
        }

        // When the Platform engine is installed its table ACLs decide (for example custom tables with own permissions).
        var probe = await records.QueryAsync(table.Name, new RecordQuery { Page = new PageRequest { PageSize = 1 }, Fields = ["Id"], IncludeDisplayValues = false }, cancellationToken);
        return probe.Succeeded;
    }

    /// <summary>Runs a report.</summary>
    /// <param name="spec">The specification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<Result<ReportResult>> RunAsync(ReportSpec spec, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var table = await registry.GetTableAsync(spec.TableName, cancellationToken);
        if (table is null)
        {
            return Result<ReportResult>.NotFound($"The table '{spec.TableName}' does not exist (is the module installed?).");
        }

        if (!await CanReadAsync(table, cancellationToken))
        {
            return Result<ReportResult>.Forbidden($"You do not have permission to report on {table.PluralLabel}.");
        }

        if (Validate(spec, table) is { } error)
        {
            return Result<ReportResult>.Invalid(error);
        }

        var groupField = string.IsNullOrEmpty(spec.GroupBy) ? null : table.FindField(spec.GroupBy);
        var valueField = spec.Aggregate == ReportAggregate.Count ? null : table.FindField(spec.AggregateField!);
        var warnings = new List<string>();
        var references = new Dictionary<Guid, string>();
        List<ReportProjection> rows;
        bool truncated;

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            if (table.EntityType is { } entityType)
            {
                if (!EntityQuery.IsMapped(db, entityType))
                {
                    return Result<ReportResult>.Failure($"{table.PluralLabel} are not available in this database.");
                }

                var filters = new List<LambdaExpression?>();
                if (spec.Condition is { IsEmpty: false } condition)
                {
                    if (ConditionSupport.IsSupported(evaluator))
                    {
                        filters.Add((LambdaExpression)s_toExpression.MakeGenericMethod(entityType).Invoke(evaluator, [condition, new ConditionContext(currentUser.UserId)])!);
                    }
                    else
                    {
                        warnings.Add("Filter conditions require the Platform engine, which is not installed. The result is unfiltered.");
                    }
                }

                rows = await EntityQuery.ProjectAsync(db, entityType, groupField?.Name, valueField?.Name, filters, MaxTypedRows + 1, cancellationToken);
                truncated = rows.Count > MaxTypedRows;
                if (truncated)
                {
                    rows.RemoveAt(rows.Count - 1);
                }

                if (groupField is { Type: FieldType.Reference, ReferenceTable: { } referenceTableName }
                    && await registry.GetTableAsync(referenceTableName, cancellationToken) is { EntityType: { } referenceType } referenceTable)
                {
                    var ids = rows.Select(r => r.Key).OfType<Guid>().Distinct().ToList();
                    references = await EntityQuery.ResolveDisplayAsync(db, referenceType, referenceTable.DisplayField, ids, cancellationToken);
                }
            }
            else
            {
                var custom = await QueryCustomAsync(table, spec, groupField, valueField, references, cancellationToken);
                if (!custom.Succeeded)
                {
                    return Result<ReportResult>.From(custom);
                }

                (rows, truncated) = custom.Value;
            }
        }

        if (truncated)
        {
            warnings.Add($"Only the first {rows.Count:N0} matching records were evaluated.");
        }

        var chronological = groupField is not null && ReportKeyFormatter.IsDateType(groupField.Type);
        var grouped = ReportAggregator.Aggregate(rows, ReportKeyFormatter.Create(groupField, spec.DateInterval, references), spec.Aggregate, spec.MaxGroups, chronological, out var total);
        stopwatch.Stop();

        return Result<ReportResult>.Success(new ReportResult
        {
            TableName = table.Name,
            TableLabel = table.PluralLabel,
            GroupByLabel = groupField?.Label,
            AggregateLabel = spec.Aggregate == ReportAggregate.Count ? "Count" : $"{spec.Aggregate} of {valueField!.Label}",
            Aggregate = spec.Aggregate,
            ChartType = spec.ChartType,
            Rows = groupField is null ? [new ReportRow("all", table.PluralLabel, Math.Round(total, 4), rows.Count)] : grouped,
            Total = Math.Round(total, 4),
            RecordCount = rows.Count,
            Truncated = truncated,
            IsDateSeries = chronological,
            Warnings = warnings,
            GeneratedAt = clock.UtcNow,
            DurationMs = stopwatch.ElapsedMilliseconds,
        });
    }

    private async Task<Result<(List<ReportProjection> Rows, bool Truncated)>> QueryCustomAsync(
        TableDefinition table,
        ReportSpec spec,
        FieldDefinition? groupField,
        FieldDefinition? valueField,
        Dictionary<Guid, string> references,
        CancellationToken cancellationToken)
    {
        var fields = new[] { groupField?.Name, valueField?.Name }.OfType<string>().ToList();
        var rows = new List<ReportProjection>();
        for (var page = 1; page <= MaxCustomPages; page++)
        {
            var result = await records.QueryAsync(table.Name, new RecordQuery
            {
                Condition = spec.Condition,
                Fields = fields,
                IncludeDisplayValues = groupField?.Type == FieldType.Reference,
                Page = new PageRequest { Page = page, PageSize = CustomPageSize },
            }, cancellationToken);
            if (!result.Succeeded)
            {
                return Result<(List<ReportProjection>, bool)>.From(result);
            }

            foreach (var record in result.Value!.Items)
            {
                object? key = null;
                if (groupField is not null && record.TryGetValue(groupField.Name, out key) && groupField.Type == FieldType.Reference
                    && Guid.TryParse(Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture), out var id))
                {
                    key = id;
                    if (record.TryGetValue(groupField.Name + ".display", out var display) && display is not null)
                    {
                        references[id] = display.ToString()!;
                    }
                }

                rows.Add(new ReportProjection { Key = key, Value = valueField is not null && record.TryGetValue(valueField.Name, out var value) ? value : null });
            }

            if (result.Value.Items.Count < CustomPageSize)
            {
                return Result<(List<ReportProjection>, bool)>.Success((rows, false));
            }
        }

        return Result<(List<ReportProjection>, bool)>.Success((rows, true));
    }
}
