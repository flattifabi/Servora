using System.Text.Json.Nodes;
using Servora.Application.Automation.Model;
using Servora.Application.Tables;

namespace Servora.Application.Automation.Actions;

/// <summary>Creates a record in any table through <see cref="IRecordService"/> (field-level validation applies).</summary>
/// <param name="records">The record service.</param>
public sealed class CreateRecordAction(IRecordService records) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "record.create";

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Create record", "Records", "plus", "success", "Create a new record in any table with values from data pills.")
    {
        Fields =
        [
            new FlowActionField("table", "Table", FlowActionFieldKind.Table) { Required = true, SupportsPills = false },
            new FlowActionField("values", "Field values", FlowActionFieldKind.RecordValues) { Required = true, TableFieldKey = "table" },
        ],
        Outputs = [new FlowActionOutput("id", "Record id"), new FlowActionOutput("table", "Table"), new FlowActionOutput("record", "Record", "table")],
    };

    /// <inheritdoc />
    public async Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var table = context.GetRaw("table")?.Trim();
        if (string.IsNullOrEmpty(table))
        {
            return FlowActionResult.Fail("No table is configured.");
        }

        var values = ResolveFieldValues(context, context.GetRaw("values"));
        if (values.Count == 0)
        {
            return FlowActionResult.Fail("No field values are configured.");
        }

        var created = await records.CreateAsync(table, values, cancellationToken);
        if (!created.Succeeded)
        {
            return FlowActionResult.Fail(created.Error ?? "The record could not be created.");
        }

        var output = new JsonObject { ["id"] = created.Value.ToString(), ["table"] = table };
        var record = await records.GetAsync(table, created.Value, cancellationToken);
        if (record.Succeeded)
        {
            output["record"] = FlowValues.ToJsonObject(record.Value);
        }

        return FlowActionResult.Success(output);
    }

    /// <summary>Resolves the field assignments of a record values field.</summary>
    /// <param name="context">The context.</param>
    /// <param name="json">The JSON array of <see cref="FlowFieldValue"/>.</param>
    /// <returns>The resolved values keyed by field.</returns>
    internal static Dictionary<string, object?> ResolveFieldValues(FlowActionContext context, string? json)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in FlowJson.TryDeserialize<List<FlowFieldValue>>(json) ?? [])
        {
            if (!string.IsNullOrWhiteSpace(assignment.Field) && result.Count < 200)
            {
                result[assignment.Field.Trim()] = FlowValues.ToRecordValue(context.Resolve(assignment.Value));
            }
        }

        return result;
    }
}
