using System.Text.Json.Nodes;
using Servora.Application.Activity;
using Servora.Application.Tables;
using Servora.Domain.Activity;

namespace Servora.Application.Automation.Actions;

/// <summary>Adds a work note or comment to the activity stream of a record.</summary>
/// <param name="activity">The activity service.</param>
public sealed class AddWorkNoteAction(IActivityService activity) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "record.note";

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Add work note", "Records", "sticky-note", "primary", "Add a work note or customer-visible comment to a record.")
    {
        Fields =
        [
            new FlowActionField("table", "Table", FlowActionFieldKind.Table) { Required = true, SupportsPills = false },
            new FlowActionField("recordId", "Record", FlowActionFieldKind.Text) { Required = true, Placeholder = "{{trigger.recordId}}" },
            new FlowActionField("kind", "Type", FlowActionFieldKind.Select) { DefaultValue = "worknote", SupportsPills = false, Options = [new FieldChoice("worknote", "Work note (internal)"), new FieldChoice("comment", "Comment (visible to caller)")] },
            new FlowActionField("text", "Text", FlowActionFieldKind.Multiline) { Required = true },
        ],
        Outputs = [new FlowActionOutput("entryId", "Activity entry id")],
    };

    /// <inheritdoc />
    public async Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var table = context.GetRaw("table")?.Trim();
        if (string.IsNullOrEmpty(table) || FlowValues.ParseGuid(context.GetText("recordId")) is not { } recordId)
        {
            return FlowActionResult.Fail("Table and record are required.");
        }

        var text = context.GetText("text").Trim();
        if (text.Length == 0)
        {
            return FlowActionResult.Fail("The note text is empty.");
        }

        var type = string.Equals(context.GetRaw("kind"), "comment", StringComparison.OrdinalIgnoreCase) ? ActivityType.Comment : ActivityType.WorkNote;
        var added = await activity.AddAsync(table, recordId, type, text.Length > 20_000 ? text[..20_000] : text, cancellationToken);
        return added.Succeeded
            ? FlowActionResult.Success(new JsonObject { ["entryId"] = added.Value!.Id.ToString() })
            : FlowActionResult.Fail(added.Error ?? "The note could not be added.");
    }
}
