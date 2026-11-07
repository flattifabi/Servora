using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Itsm;

namespace Servora.Application.Portal;

/// <summary>
/// Translates the fulfiller-side states of incidents and requests into consumer-friendly portal statuses and status
/// timelines (pure logic).
/// </summary>
public static class PortalStatusMapper
{
    /// <summary>Gets the portal status of an incident.</summary>
    /// <param name="state">The incident state.</param>
    /// <param name="onHoldReason">The on-hold reason.</param>
    /// <returns>The status.</returns>
    public static PortalStatus ForIncident(IncidentState state, OnHoldReason? onHoldReason = null) => state switch
    {
        IncidentState.New => new PortalStatus("Submitted", "info", true),
        IncidentState.InProgress => new PortalStatus("In progress", "primary", true),
        IncidentState.OnHold when onHoldReason == OnHoldReason.AwaitingCaller => new PortalStatus("Waiting for your reply", "warning", true, true),
        IncidentState.OnHold => new PortalStatus("On hold", "warning", true),
        IncidentState.Resolved => new PortalStatus("Resolved – please confirm", "success", true, true),
        IncidentState.Closed => new PortalStatus("Closed", "success", false),
        IncidentState.Cancelled => new PortalStatus("Cancelled", "neutral", false),
        _ => new PortalStatus(state.ToString(), "neutral", true),
    };

    /// <summary>Gets the portal status of a request.</summary>
    /// <param name="state">The request state.</param>
    /// <param name="awaitingApproval">Whether at least one item is waiting for approval.</param>
    /// <returns>The status.</returns>
    public static PortalStatus ForRequest(RequestState state, bool awaitingApproval) => state switch
    {
        RequestState.Submitted when awaitingApproval => new PortalStatus("Waiting for approval", "warning", true),
        RequestState.Submitted => new PortalStatus("Submitted", "info", true),
        RequestState.InProgress => new PortalStatus("In progress", "primary", true),
        RequestState.Completed => new PortalStatus("Completed", "success", false),
        RequestState.Rejected => new PortalStatus("Rejected", "danger", false),
        RequestState.Cancelled => new PortalStatus("Cancelled", "neutral", false),
        _ => new PortalStatus(state.ToString(), "neutral", true),
    };

    /// <summary>Gets the portal status of a requested item.</summary>
    /// <param name="state">The requested item state.</param>
    /// <returns>The status.</returns>
    public static PortalStatus ForRequestItem(RequestItemState state) => state switch
    {
        RequestItemState.PendingApproval => new PortalStatus("Waiting for approval", "warning", true),
        RequestItemState.InFulfillment => new PortalStatus("In progress", "primary", true),
        RequestItemState.Completed => new PortalStatus("Delivered", "success", false),
        RequestItemState.Rejected => new PortalStatus("Rejected", "danger", false),
        RequestItemState.Cancelled => new PortalStatus("Cancelled", "neutral", false),
        _ => new PortalStatus(state.ToString(), "neutral", true),
    };

    /// <summary>Gets the requester-facing label of an approval status.</summary>
    /// <param name="status">The approval status.</param>
    /// <returns>The label, or <see langword="null"/> when no approval is involved.</returns>
    public static string? ApprovalLabel(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Requested => "Waiting for approval",
        ApprovalStatus.Approved => "Approved",
        ApprovalStatus.Rejected => "Rejected",
        ApprovalStatus.Cancelled => "Approval withdrawn",
        _ => null,
    };

    /// <summary>Builds the status timeline of an incident.</summary>
    /// <param name="state">The incident state.</param>
    /// <param name="onHoldReason">The on-hold reason.</param>
    /// <param name="openedAt">The UTC opening time.</param>
    /// <param name="resolvedAt">The UTC resolution time.</param>
    /// <param name="closedAt">The UTC closing time.</param>
    /// <returns>The steps in order.</returns>
    public static IReadOnlyList<PortalProgressStep> IncidentSteps(IncidentState state, OnHoldReason? onHoldReason, DateTimeOffset openedAt, DateTimeOffset? resolvedAt, DateTimeOffset? closedAt)
    {
        if (state == IncidentState.Cancelled)
        {
            return
            [
                new PortalProgressStep("Submitted", "We received your issue.", PortalStepState.Completed, openedAt),
                new PortalProgressStep("Cancelled", "The issue was cancelled and needs no further work.", PortalStepState.Failed, closedAt),
            ];
        }

        var working = state is IncidentState.InProgress or IncidentState.OnHold;
        var resolved = state is IncidentState.Resolved or IncidentState.Closed;
        var workingText = state == IncidentState.OnHold
            ? onHoldReason == OnHoldReason.AwaitingCaller ? "The team is waiting for your reply – please add a comment." : "Work is paused while the team waits for others."
            : "A support specialist is working on it.";

        return
        [
            new PortalProgressStep("Submitted", state == IncidentState.New ? "Waiting for the service desk to pick it up." : "We received your issue.",
                state == IncidentState.New ? PortalStepState.Current : PortalStepState.Completed, openedAt),
            new PortalProgressStep("In progress", working ? workingText : resolved ? "The team worked on your issue." : "A specialist will start working on it.",
                working ? PortalStepState.Current : resolved ? PortalStepState.Completed : PortalStepState.Upcoming),
            new PortalProgressStep("Resolved", state == IncidentState.Resolved ? "Please confirm that the problem is solved or reopen it." : resolved ? "A solution was provided." : "You will be asked to confirm the solution.",
                state == IncidentState.Resolved ? PortalStepState.Current : resolved ? PortalStepState.Completed : PortalStepState.Upcoming, resolvedAt),
            new PortalProgressStep("Closed", state == IncidentState.Closed ? "The issue is closed." : "The issue closes after confirmation.",
                state == IncidentState.Closed ? PortalStepState.Completed : PortalStepState.Upcoming, closedAt),
        ];
    }

    /// <summary>Builds the status timeline of a request.</summary>
    /// <param name="state">The request state.</param>
    /// <param name="approvalInvolved">Whether any item of the request needs or needed an approval.</param>
    /// <param name="openedAt">The UTC order time.</param>
    /// <param name="closedAt">The UTC closing time.</param>
    /// <returns>The steps in order.</returns>
    public static IReadOnlyList<PortalProgressStep> RequestSteps(RequestState state, bool approvalInvolved, DateTimeOffset openedAt, DateTimeOffset? closedAt)
    {
        var steps = new List<PortalProgressStep>
        {
            new("Submitted", "We received your order.", state == RequestState.Submitted && !approvalInvolved ? PortalStepState.Current : PortalStepState.Completed, openedAt),
        };

        if (approvalInvolved || state == RequestState.Rejected)
        {
            var approvalState = state switch
            {
                RequestState.Submitted => PortalStepState.Current,
                RequestState.Rejected => PortalStepState.Failed,
                RequestState.Cancelled => PortalStepState.Upcoming,
                _ => PortalStepState.Completed,
            };
            var text = approvalState switch
            {
                PortalStepState.Current => "Waiting for the approvers' decision.",
                PortalStepState.Failed => "The request was not approved.",
                PortalStepState.Completed => "The request was approved.",
                _ => "The approvers decide on the request.",
            };
            steps.Add(new PortalProgressStep(state == RequestState.Rejected ? "Rejected" : "Approval", text, approvalState, state == RequestState.Rejected ? closedAt : null));
            if (state == RequestState.Rejected)
            {
                return steps;
            }
        }

        if (state == RequestState.Cancelled)
        {
            steps.Add(new PortalProgressStep("Cancelled", "The request was cancelled.", PortalStepState.Failed, closedAt));
            return steps;
        }

        steps.Add(new PortalProgressStep("Fulfilment", state switch
            {
                RequestState.InProgress => "The fulfilment team is preparing your order.",
                RequestState.Completed => "Your order was prepared.",
                _ => "The fulfilment team prepares your order.",
            },
            state switch { RequestState.InProgress => PortalStepState.Current, RequestState.Completed => PortalStepState.Completed, _ => PortalStepState.Upcoming }));
        steps.Add(new PortalProgressStep("Delivered", state == RequestState.Completed ? "Everything was delivered." : "You will be notified when everything is delivered.",
            state == RequestState.Completed ? PortalStepState.Completed : PortalStepState.Upcoming, state == RequestState.Completed ? closedAt : null));
        return steps;
    }
}
