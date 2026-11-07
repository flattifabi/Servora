using Servora.Application.Portal;
using Servora.Domain.Catalog;
using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.Portal;

/// <summary>Pure portal logic: status mapping, timelines, link policy and input validation.</summary>
public sealed class PortalLogicTests
{
    private static readonly DateTimeOffset s_opened = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("/portal/knowledge", true)]
    [InlineData("/", true)]
    [InlineData("https://status.example.com/incidents", true)]
    [InlineData("//evil.example.com", false)]
    [InlineData("/\\evil.example.com", false)]
    [InlineData("http://example.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("JavaScript:alert(1)", false)]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=", false)]
    [InlineData("https://user:pass@example.com", false)]
    [InlineData("https://example.com/a b", false)]
    [InlineData("portal/knowledge", false)]
    [InlineData("", false)]
    public void LinkPolicy_AllowsOnlySiteRelativeAndHttps(string link, bool allowed) =>
        Assert.Equal(allowed, PortalLinkPolicy.IsAllowed(link));

    [Fact]
    public void IncidentStatus_ResolvedNeedsCallerAction_AwaitingCallerAsksForReply()
    {
        Assert.True(PortalStatusMapper.ForIncident(IncidentState.Resolved).NeedsAction);
        Assert.True(PortalStatusMapper.ForIncident(IncidentState.Resolved).IsOpen);
        Assert.Equal("Waiting for your reply", PortalStatusMapper.ForIncident(IncidentState.OnHold, OnHoldReason.AwaitingCaller).Label);
        Assert.False(PortalStatusMapper.ForIncident(IncidentState.OnHold, OnHoldReason.AwaitingVendor).NeedsAction);
        Assert.False(PortalStatusMapper.ForIncident(IncidentState.Closed).IsOpen);
    }

    [Fact]
    public void IncidentSteps_MarkCurrentAndCompletedSteps()
    {
        var resolvedAt = s_opened.AddHours(3);
        var steps = PortalStatusMapper.IncidentSteps(IncidentState.Resolved, null, s_opened, resolvedAt, null);
        Assert.Equal(["Submitted", "In progress", "Resolved", "Closed"], steps.Select(s => s.Label).ToArray());
        Assert.Equal([PortalStepState.Completed, PortalStepState.Completed, PortalStepState.Current, PortalStepState.Upcoming], steps.Select(s => s.State).ToArray());
        Assert.Equal(resolvedAt, steps[2].At);

        var cancelled = PortalStatusMapper.IncidentSteps(IncidentState.Cancelled, null, s_opened, null, s_opened.AddHours(1));
        Assert.Equal(PortalStepState.Failed, cancelled[^1].State);
    }

    [Fact]
    public void RequestSteps_IncludeApprovalOnlyWhenInvolved()
    {
        var withoutApproval = PortalStatusMapper.RequestSteps(RequestState.InProgress, false, s_opened, null);
        Assert.Equal(["Submitted", "Fulfilment", "Delivered"], withoutApproval.Select(s => s.Label).ToArray());

        var awaiting = PortalStatusMapper.RequestSteps(RequestState.Submitted, true, s_opened, null);
        Assert.Equal(PortalStepState.Current, awaiting.Single(s => s.Label == "Approval").State);

        var rejected = PortalStatusMapper.RequestSteps(RequestState.Rejected, true, s_opened, s_opened.AddDays(1));
        Assert.Equal(["Submitted", "Rejected"], rejected.Select(s => s.Label).ToArray());
        Assert.Equal(PortalStepState.Failed, rejected[^1].State);

        var completed = PortalStatusMapper.RequestSteps(RequestState.Completed, true, s_opened, s_opened.AddDays(2));
        Assert.All(completed, s => Assert.Equal(PortalStepState.Completed, s.State));
        Assert.Equal("Waiting for approval", PortalStatusMapper.ForRequest(RequestState.Submitted, true).Label);
        Assert.Equal("Delivered", PortalStatusMapper.ForRequestItem(RequestItemState.Completed).Label);
    }

    [Theory]
    [InlineData("Help", false)]
    [InlineData("Printer broken", true)]
    public void IssueInput_RequiresMeaningfulSummary(string summary, bool valid) =>
        Assert.Equal(valid, new PortalIssueInput { ShortDescription = summary }.Validate() is null);

    [Fact]
    public void AnnouncementInput_RejectsEndBeforeStart()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.NotNull(new PortalAnnouncementInput { Title = "x", StartsAt = now, EndsAt = now.AddMinutes(-1) }.Validate());
        Assert.Null(new PortalAnnouncementInput { Title = "x", StartsAt = now, EndsAt = now.AddMinutes(1) }.Validate());
    }
}
