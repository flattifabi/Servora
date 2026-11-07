using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Servora.Application.Approvals;
using Servora.Application.Automation.Engine;
using Servora.Application.Tables;

namespace Servora.Application.Automation.Actions;

/// <summary>
/// Requests an approval for a record through <see cref="IApprovalService"/> and waits for the decision. Continues on
/// the "approved" or "rejected" lane (a timeout or cancellation counts as rejected).
/// </summary>
/// <param name="approvals">The approval service.</param>
/// <param name="options">The engine options.</param>
public sealed class ApprovalAction(IApprovalService approvals, IOptions<FlowEngineOptions> options) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "approval.request";

    /// <summary>The branch executed after approval.</summary>
    public const string ApprovedBranch = "approved";

    /// <summary>The branch executed after rejection, cancellation or timeout.</summary>
    public const string RejectedBranch = "rejected";

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Ask for approval", "Approvals", "check-square", "purple", "Request approval from users or groups and wait for the decision.")
    {
        Fields =
        [
            new FlowActionField("table", "Table", FlowActionFieldKind.Table) { Required = true, SupportsPills = false },
            new FlowActionField("recordId", "Record", FlowActionFieldKind.Text) { Required = true, Placeholder = "{{trigger.recordId}}" },
            new FlowActionField("approvers", "Approver users", FlowActionFieldKind.Text) { Help = "Comma separated user ids or data pills." },
            new FlowActionField("groups", "Approval groups", FlowActionFieldKind.Text) { Help = "Comma separated group ids or data pills." },
            new FlowActionField("policy", "Rule", FlowActionFieldKind.Select) { DefaultValue = "any", SupportsPills = false, Options = [new FieldChoice("any", "First response decides"), new FieldChoice("all", "Everyone must approve")] },
            new FlowActionField("reason", "Message to approvers", FlowActionFieldKind.Multiline),
            new FlowActionField("timeoutHours", "Timeout (hours, 0 = none)", FlowActionFieldKind.Number) { DefaultValue = "0" },
        ],
        Outputs = [new FlowActionOutput("state", "Decision"), new FlowActionOutput("approved", "Approved count"), new FlowActionOutput("rejected", "Rejected count")],
        Branches = [new FlowActionBranch(ApprovedBranch, "Approved", "success"), new FlowActionBranch(RejectedBranch, "Rejected", "danger")],
        CanWait = true,
    };

    /// <inheritdoc />
    public async Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var table = context.GetRaw("table")?.Trim();
        if (string.IsNullOrEmpty(table) || FlowValues.ParseGuid(context.GetText("recordId")) is not { } recordId)
        {
            return FlowActionResult.Fail("Table and record are required for an approval.");
        }

        var poll = TimeSpan.FromSeconds(Math.Clamp(options.Value.ApprovalPollSeconds, 5, 3600));
        if (!context.IsResume)
        {
            var users = FlowValues.ParseGuids(context.GetText("approvers"));
            var groups = FlowValues.ParseGuids(context.GetText("groups"));
            if (users.Count == 0 && groups.Count == 0)
            {
                return FlowActionResult.Fail("At least one approver user or group is required.");
            }

            var policy = string.Equals(context.GetRaw("policy"), "all", StringComparison.OrdinalIgnoreCase) ? ApprovalPolicy.AllApprovers : ApprovalPolicy.AnyApprover;
            var reason = Engine.FlowRedactor.Truncate(context.GetText("reason"), 2000);
            var requested = await approvals.RequestAsync(new ApprovalRequest(table, recordId, users, groups, policy, string.IsNullOrWhiteSpace(reason) ? null : reason), cancellationToken);
            if (!requested.Succeeded)
            {
                return FlowActionResult.Fail(requested.Error ?? "The approval could not be requested.");
            }

            var timeoutHours = Math.Clamp(context.GetInt("timeoutHours", 0), 0, 24 * 90);
            var state = new JsonObject
            {
                ["table"] = table,
                ["recordId"] = recordId.ToString(),
                ["requestedAt"] = context.Now.ToString("O", CultureInfo.InvariantCulture),
                ["timeoutAt"] = timeoutHours > 0 ? context.Now.AddHours(timeoutHours).ToString("O", CultureInfo.InvariantCulture) : null,
            };
            return FlowActionResult.Wait(context.Now + poll, "Waiting for approval", state);
        }

        var summary = await approvals.GetSummaryAsync(table, recordId, cancellationToken);
        switch (summary.State)
        {
            case ApprovalState.Approved:
                return FlowActionResult.Success(Output("Approved", summary), ApprovedBranch);
            case ApprovalState.Rejected:
            case ApprovalState.Cancelled:
                return FlowActionResult.Success(Output(summary.State.ToString(), summary), RejectedBranch);
        }

        var timeoutText = context.WaitState?["timeoutAt"] is JsonValue t ? Templates.FlowTemplate.ToText(t) : null;
        if (DateTimeOffset.TryParse(timeoutText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timeoutAt) && context.Now >= timeoutAt)
        {
            await approvals.CancelAsync(table, recordId, cancellationToken);
            return FlowActionResult.Success(Output("TimedOut", summary), RejectedBranch);
        }

        return FlowActionResult.Wait(context.Now + poll, $"Waiting for approval ({summary.Pending} pending)", context.WaitState);
    }

    private static JsonObject Output(string state, ApprovalSummary summary) => new()
    {
        ["state"] = state,
        ["approved"] = summary.Approved,
        ["rejected"] = summary.Rejected,
        ["pending"] = summary.Pending,
    };
}
