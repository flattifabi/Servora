using System.Text.Json.Nodes;
using Servora.Application.Tables;

namespace Servora.Application.Automation.Actions;

/// <summary>Updates fields of an existing record through <see cref="IRecordService"/>.</summary>
/// <param name="records">The record service.</param>
public sealed class UpdateRecordAction(IRecordService records) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "record.update";

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Update record", "Records", "edit", "primary", "Change field values of a record, for example the triggering record.")
    {
        Fields =
        [
            new FlowActionField("table", "Table", FlowActionFieldKind.Table) { Required = true, SupportsPills = false },
            new FlowActionField("recordId", "Record", FlowActionFieldKind.Text) { Required = true, Placeholder = "{{trigger.recordId}}", Help = "Record id, usually a data pill." },
            new FlowActionField("values", "Field values", FlowActionFieldKind.RecordValues) { Required = true, TableFieldKey = "table" },
        ],
        Outputs = [new FlowActionOutput("id", "Record id"), new FlowActionOutput("record", "Updated record", "table")],
    };

    /// <inheritdoc />
    public async Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var table = context.GetRaw("table")?.Trim();
        if (string.IsNullOrEmpty(table))
        {
            return FlowActionResult.Fail("No table is configured.");
        }

        if (FlowValues.ParseGuid(context.GetText("recordId")) is not { } recordId)
        {
            return FlowActionResult.Fail("The record id is empty or invalid.");
        }

        var values = CreateRecordAction.ResolveFieldValues(context, context.GetRaw("values"));
        if (values.Count == 0)
        {
            return FlowActionResult.Fail("No field values are configured.");
        }

        var updated = await records.UpdateAsync(table, recordId, values, cancellationToken);
        if (!updated.Succeeded)
        {
            return FlowActionResult.Fail(updated.Error ?? "The record could not be updated.", retryable: updated.ErrorKind == Common.ResultErrorKind.Conflict);
        }

        var output = new JsonObject { ["id"] = recordId.ToString(), ["table"] = table };
        var record = await records.GetAsync(table, recordId, cancellationToken);
        if (record.Succeeded)
        {
            output["record"] = FlowValues.ToJsonObject(record.Value);
        }

        return FlowActionResult.Success(output);
    }
}
