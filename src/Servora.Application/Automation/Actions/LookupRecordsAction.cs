using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Tables;

namespace Servora.Application.Automation.Actions;

/// <summary>Looks up records with a filter (values may contain data pills). Results are bounded.</summary>
/// <param name="records">The record service.</param>
public sealed class LookupRecordsAction(IRecordService records) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "record.lookup";

    /// <summary>The maximum number of returned records.</summary>
    public const int MaxRecords = 100;

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Look up records", "Records", "search", "info", "Find records matching a filter and use them in later steps.")
    {
        Fields =
        [
            new FlowActionField("table", "Table", FlowActionFieldKind.Table) { Required = true, SupportsPills = false },
            new FlowActionField("condition", "Filter", FlowActionFieldKind.RecordCondition) { TableFieldKey = "table", Help = "Values may contain data pills." },
            new FlowActionField("limit", "Maximum records", FlowActionFieldKind.Number) { DefaultValue = "10", Help = "1 – 100." },
        ],
        Outputs = [new FlowActionOutput("count", "Number of records"), new FlowActionOutput("total", "Total matches"), new FlowActionOutput("first", "First record", "table"), new FlowActionOutput("records", "Records")],
    };

    /// <inheritdoc />
    public async Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var table = context.GetRaw("table")?.Trim();
        if (string.IsNullOrEmpty(table))
        {
            return FlowActionResult.Fail("No table is configured.");
        }

        var condition = ConditionSerializer.Deserialize(context.GetRaw("condition"));
        ResolveValues(condition, context);
        var limit = Math.Clamp(context.GetInt("limit", 10), 1, MaxRecords);
        var query = new RecordQuery { Condition = condition, Page = new PageRequest { Page = 1, PageSize = limit }, IncludeDisplayValues = true };
        var result = await records.QueryAsync(table, query, cancellationToken);
        if (!result.Succeeded)
        {
            return FlowActionResult.Fail(result.Error ?? "The lookup failed.");
        }

        var list = new JsonArray();
        foreach (var record in result.Value!.Items)
        {
            list.Add(FlowValues.ToJsonObject(record));
        }

        var output = new JsonObject
        {
            ["count"] = list.Count,
            ["total"] = result.Value.TotalCount,
            ["first"] = list.Count > 0 ? list[0]!.DeepClone() : null,
            ["records"] = list,
        };
        return FlowActionResult.Success(output);
    }

    private static void ResolveValues(ConditionGroup group, FlowActionContext context)
    {
        foreach (var rule in group.Rules)
        {
            rule.Value = rule.Value is null ? null : context.Render(rule.Value);
        }

        foreach (var child in group.Groups)
        {
            ResolveValues(child, context);
        }
    }
}
