using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Approvals;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Scheduling;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Domain.Automation;
using static Servora.Tests.Unit.Automation.TestSteps;

namespace Servora.Tests.Unit.Automation;

/// <summary>
/// End-to-end acceptance scenarios of T-08: record-triggered flow with condition, update and e-mail; scheduled flow;
/// approval wait and resume; data source call inside a flow.
/// </summary>
public sealed class AutomationScenarioTests
{
    private static ConditionGroup PriorityIsOne() => new() { Rules = [new ConditionRule { Field = "Priority", Operator = ConditionOperator.Equals, Value = "1" }] };

    private static async Task<Flow> AddEscalationFlowAsync(AutomationTestHost host) =>
        await host.AddFlowAsync(
            [
                If(
                    "notResolved",
                    Rule("{{trigger.record.State}}", FlowConditionOperator.NotEquals, "Resolved"),
                    [
                        Step(
                            "escalate",
                            UpdateRecordAction.TypeKey,
                            ("table", "incident"),
                            ("recordId", "{{trigger.recordId}}"),
                            ("values", FlowJson.Serialize(new List<FlowFieldValue>
                            {
                                new() { Field = "AssignmentGroup", Value = "Major Incident Team" },
                                new() { Field = "State", Value = "In progress" },
                            }))),
                        Step(
                            "notify",
                            SendEmailAction.TypeKey,
                            ("to", "{{trigger.record.CallerEmail}}"),
                            ("subject", "{{trigger.record.Number}} escalated to {{steps.escalate.record.AssignmentGroup}}"),
                            ("body", "Hello, your incident '{{trigger.record.ShortDescription}}' is now handled by the major incident team.")),
                    ],
                    [Step("skip", LogAction.TypeKey, ("message", "Already resolved"))]),
            ],
            f =>
            {
                f.Name = "Escalate P1 incidents";
                f.TriggerType = FlowTriggerType.RecordUpdated;
                f.TriggerTable = "incident";
                f.TriggerConditionJson = ConditionSerializer.Serialize(PriorityIsOne());
            });

    private static RecordChangedEvent Updated(Guid id, IReadOnlyDictionary<string, object?> snapshot, DateTimeOffset at, string actor = "Service Desk Agent") =>
        new("incident", id, RecordChangeType.Updated, [new RecordFieldChange("Priority", 3, snapshot["Priority"])], snapshot, Guid.CreateVersion7(), actor, at, 0);

    private static async Task<List<FlowRun>> RunsOfAsync(AutomationTestHost host, Guid flowId)
    {
        await using var db = host.Create();
        return await db.Set<FlowRun>().AsNoTracking().Where(r => r.FlowId == flowId).ToListAsync();
    }

    private static async Task DispatchAsync(AutomationTestHost host, RecordChangedEvent recordEvent)
    {
        await using var scope = host.Services.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IRecordEventHandler>())
        {
            await handler.HandleAsync(recordEvent, CancellationToken.None);
        }
    }

    [Fact]
    public async Task RecordTriggeredFlow_WithConditionUpdateAndEmail_RunsEndToEnd()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await AddEscalationFlowAsync(host);
        var snapshot = new Dictionary<string, object?>
        {
            ["Number"] = "INC0001001",
            ["ShortDescription"] = "Mail server down",
            ["Priority"] = 1,
            ["State"] = "New",
            ["CallerEmail"] = "ada@example.com",
            ["PasswordHash"] = "must-never-leak",
        };
        var incidentId = host.Records.Seed("incident", snapshot);

        await DispatchAsync(host, Updated(incidentId, snapshot, host.Clock.UtcNow));

        var run = Assert.Single(await RunsOfAsync(host, flow.Id));
        Assert.Equal(FlowTriggerType.RecordUpdated, run.TriggerType);
        Assert.Equal(incidentId, run.TriggerRecordId);
        Assert.DoesNotContain("must-never-leak", run.ContextJson);

        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(run.Id));

        var update = Assert.Single(host.Records.Updates);
        Assert.Equal(incidentId, update.Id);
        Assert.Equal("Major Incident Team", update.Values["AssignmentGroup"]);
        Assert.Equal("In progress", update.Values["State"]);
        var mail = Assert.Single(host.Email.Sent);
        Assert.Equal(["ada@example.com"], mail.To);
        Assert.Equal("INC0001001 escalated to Major Incident Team", mail.Subject);
        Assert.Contains("Mail server down", mail.HtmlBody);
        Assert.Equal(["notResolved", "escalate", "notify"], (await host.GetStepsAsync(run.Id)).Select(s => s.StepKey));
    }

    [Fact]
    public async Task RecordTrigger_RespectsConditionChangeTypeSelfTriggerAndIdempotency()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await AddEscalationFlowAsync(host);
        var low = new Dictionary<string, object?> { ["Number"] = "INC0001002", ["Priority"] = 3, ["State"] = "New" };
        var high = new Dictionary<string, object?> { ["Number"] = "INC0001003", ["Priority"] = 1, ["State"] = "New" };
        var lowId = host.Records.Seed("incident", low);
        var highId = host.Records.Seed("incident", high);
        var at = host.Clock.UtcNow;

        await DispatchAsync(host, Updated(lowId, low, at));
        await DispatchAsync(host, Updated(highId, high, at, actor: "Flow: Escalate P1 incidents"));
        await DispatchAsync(host, new RecordChangedEvent("incident", highId, RecordChangeType.Created, [], high, null, "Agent", at, 0));
        await DispatchAsync(host, new RecordChangedEvent("problem", highId, RecordChangeType.Updated, [], high, null, "Agent", at, 0));
        Assert.Empty(await RunsOfAsync(host, flow.Id));

        var delivery = Updated(highId, high, at.AddSeconds(5));
        await DispatchAsync(host, delivery);
        await DispatchAsync(host, delivery);
        Assert.Single(await RunsOfAsync(host, flow.Id));
    }

    [Fact]
    public async Task ScheduledFlow_FiresWhenDue_AndAdvancesNextRun()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync(
            [Set("tick", "firedAt", "{{trigger.scheduledAt}}")],
            f =>
            {
                f.TriggerType = FlowTriggerType.Schedule;
                f.CronExpression = "*/15 * * * *";
                f.NextRunAt = host.Clock.UtcNow.AddMinutes(-1);
            });

        var fired = await host.WithAsync<ScheduleProcessor, int>(p => p.ProcessDueAsync(CancellationToken.None));
        var again = await host.WithAsync<ScheduleProcessor, int>(p => p.ProcessDueAsync(CancellationToken.None));

        Assert.Equal(1, fired);
        Assert.Equal(0, again);
        var run = Assert.Single(await RunsOfAsync(host, flow.Id));
        Assert.Equal(FlowTriggerType.Schedule, run.TriggerType);
        Assert.Equal("Scheduler", run.StartedBy);
        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(run.Id));

        await using var db = host.Create();
        var stored = await db.Set<Flow>().AsNoTracking().SingleAsync(f => f.Id == flow.Id);
        Assert.Equal(host.Clock.UtcNow.AddMinutes(15), stored.NextRunAt);
        Assert.Equal(host.Clock.UtcNow, stored.LastTriggeredAt);
    }

    [Fact]
    public async Task ScheduledFlows_AreListedWithNextRunPreview()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        await host.AddFlowAsync([Set("a", "x", "1")], f =>
        {
            f.Name = "Nightly cleanup";
            f.TriggerType = FlowTriggerType.Schedule;
            f.CronExpression = "0 2 * * *";
            f.NextRunAt = host.Clock.UtcNow.AddHours(18);
        });

        var list = await host.WithAsync<IScheduleService, Result<IReadOnlyList<ScheduleItem>>>(s => s.ListAsync());
        var preview = await host.WithAsync<IScheduleService, Result<IReadOnlyList<DateTimeOffset>>>(s => Task.FromResult(s.Preview("0 2 * * *", "UTC", 3)));
        var invalid = await host.WithAsync<IScheduleService, Result<IReadOnlyList<DateTimeOffset>>>(s => Task.FromResult(s.Preview("99 * * * *", null)));

        var item = Assert.Single(list.Value!);
        Assert.Equal("Nightly cleanup", item.Name);
        Assert.Equal(ScheduleKind.Flow, item.Kind);
        Assert.Equal(3, preview.Value!.Count);
        Assert.Equal(ResultErrorKind.Validation, invalid.ErrorKind);
    }

    [Fact]
    public async Task ApprovalStep_WaitsForDecision_AndResumesOnApproval()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var incidentId = host.Records.Seed("incident", new Dictionary<string, object?> { ["Number"] = "INC0001009" });
        var approver = Guid.CreateVersion7();
        var approval = Step(
            "approval",
            ApprovalAction.TypeKey,
            ("table", "incident"),
            ("recordId", incidentId.ToString()),
            ("approvers", approver.ToString()),
            ("policy", "any"),
            ("reason", "Please approve the emergency fix"));
        approval.Branches =
        [
            new FlowBranchDefinition { Key = ApprovalAction.ApprovedBranch, Steps = [Set("ok", "decision", "approved")] },
            new FlowBranchDefinition { Key = ApprovalAction.RejectedBranch, Steps = [Set("nok", "decision", "rejected")] },
        ];
        var flow = await host.AddFlowAsync([approval, Set("done", "finished", "{{steps.approval.state}}")]);
        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Waiting, await host.RunAsync(runId));
        var request = Assert.Single(host.Approvals.Requests);
        Assert.Equal([approver], request.ApproverUserIds);
        Assert.Equal("Please approve the emergency fix", request.Reason);

        host.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(FlowRunStatus.Waiting, await host.RunAsync(runId));

        host.Approvals.State = ApprovalState.Approved;
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));

        Assert.Single(host.Approvals.Requests);
        var context = FlowRunContext.Parse((await host.GetRunAsync(runId)).ContextJson);
        Assert.Equal("approved", context.Variables["decision"]!.GetValue<string>());
        Assert.Equal("Approved", context.Variables["finished"]!.GetValue<string>());
        Assert.DoesNotContain(await host.GetStepsAsync(runId), s => s.StepKey == "nok");
    }

    [Fact]
    public async Task ApprovalStep_TakesRejectedLane()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var incidentId = host.Records.Seed("incident", new Dictionary<string, object?> { ["Number"] = "INC0001010" });
        var approval = Step("approval", ApprovalAction.TypeKey, ("table", "incident"), ("recordId", incidentId.ToString()), ("approvers", Guid.CreateVersion7().ToString()));
        approval.Branches =
        [
            new FlowBranchDefinition { Key = ApprovalAction.ApprovedBranch, Steps = [Set("ok", "decision", "approved")] },
            new FlowBranchDefinition { Key = ApprovalAction.RejectedBranch, Steps = [Set("nok", "decision", "rejected")] },
        ];
        var flow = await host.AddFlowAsync([approval]);
        var runId = await host.StartAsync(flow.Id);
        Assert.Equal(FlowRunStatus.Waiting, await host.RunAsync(runId));

        host.Approvals.State = ApprovalState.Rejected;
        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));

        Assert.Equal("rejected", FlowRunContext.Parse((await host.GetRunAsync(runId)).ContextJson).Variables["decision"]!.GetValue<string>());
    }

    [Fact]
    public async Task CallDataSourceStep_UsesApprovedEndpointAndJsonResponse()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var (sourceId, _) = await IntegrationSeed.AddDataSourceAsync(host);
        host.Invoker.Body = """{ "login": "octocat", "followers": 42 }""";
        var flow = await host.AddFlowAsync(
        [
            Step("lookup", CallDataSourceAction.TypeKey, ("dataSourceId", sourceId.ToString()), ("endpoint", "get_user"), ("parameters", """{ "user": "octocat" }""")),
            Set("followers", "count", "{{steps.lookup.json.followers}}"),
        ]);

        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));
        Assert.Equal(1, host.Invoker.Calls);
        Assert.Equal(42, FlowRunContext.Parse((await host.GetRunAsync(runId)).ContextJson).Variables["count"]!.GetValue<int>());
    }
}
