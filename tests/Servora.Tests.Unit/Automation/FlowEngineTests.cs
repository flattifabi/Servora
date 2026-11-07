using System.Text.Json.Nodes;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Services;
using Servora.Application.Common;
using Servora.Domain.Automation;
using Servora.Tests.Unit.Automation.Fakes;
using static Servora.Tests.Unit.Automation.TestSteps;

namespace Servora.Tests.Unit.Automation;

/// <summary>Step sequencing, branching, waiting / resume, retries, error handling and cancellation of the flow engine.</summary>
public sealed class FlowEngineTests
{
    [Fact]
    public async Task LinearSteps_RunInOrder_AndShareDataPills()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync(
        [
            Set("first", "greeting", "Hello"),
            Set("second", "message", "{{vars.greeting}} from {{flow.name}}"),
            Step("log", LogAction.TypeKey, ("message", "{{steps.second.value}}")),
        ]);

        var runId = await host.StartAsync(flow.Id);
        var status = await host.RunAsync(runId);

        Assert.Equal(FlowRunStatus.Succeeded, status);
        var steps = await host.GetStepsAsync(runId);
        Assert.Equal(["first", "second", "log"], steps.Select(s => s.StepKey));
        Assert.All(steps, s => Assert.Equal(FlowStepStatus.Succeeded, s.Status));
        Assert.Equal([1, 2, 3], steps.Select(s => s.Sequence));
        var context = FlowRunContext.Parse((await host.GetRunAsync(runId)).ContextJson);
        Assert.Equal($"Hello from {flow.Name}", context.Variables["message"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("1", "then")]
    [InlineData("3", "else")]
    public async Task Condition_RunsOnlyTheMatchingLane(string priority, string expectedLane)
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync(
        [
            Set("prio", "priority", priority),
            If(
                "check",
                Rule("{{vars.priority}}", FlowConditionOperator.LessOrEqual, "2"),
                [Set("urgent", "lane", "then")],
                [Set("normal", "lane", "else")]),
            Set("after", "done", "yes"),
        ]);

        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));
        var keys = (await host.GetStepsAsync(runId)).Select(s => s.StepKey).ToList();
        Assert.Equal(["prio", "check", expectedLane == "then" ? "urgent" : "normal", "after"], keys);
        var context = FlowRunContext.Parse((await host.GetRunAsync(runId)).ContextJson);
        Assert.Equal(expectedLane, context.Variables["lane"]!.GetValue<string>());
        Assert.Equal(expectedLane == "then", context.Steps["check"]!["result"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Wait_PausesRun_AndResumesAfterDueTime_WithoutRepeatingCompletedSteps()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync(
        [
            Set("before", "phase", "before"),
            Step("pause", WaitAction.TypeKey, ("mode", "duration"), ("minutes", "30")),
            Set("after", "phase", "after"),
        ]);
        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Waiting, await host.RunAsync(runId));
        var waiting = await host.GetRunAsync(runId);
        Assert.Equal(host.Clock.UtcNow.AddMinutes(30), waiting.ResumeAt);
        Assert.Contains("pause", waiting.WaitReason);

        // Resuming too early keeps waiting.
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(FlowRunStatus.Waiting, await host.RunAsync(runId));

        host.Clock.Advance(TimeSpan.FromMinutes(25));
        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));

        var steps = await host.GetStepsAsync(runId);
        Assert.Equal(1, steps.Count(s => s.StepKey == "before"));
        Assert.Equal(1, steps.Count(s => s.StepKey == "after"));
        Assert.Equal(FlowStepStatus.Succeeded, steps.Single(s => s.StepKey == "pause").Status);
        Assert.Equal("after", FlowRunContext.Parse((await host.GetRunAsync(runId)).ContextJson).Variables["phase"]!.GetValue<string>());
    }

    [Fact]
    public async Task RetryableFailure_IsRetriedUntilSuccess()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        host.Flaky.FailuresBeforeSuccess = 2;
        var step = Step("flaky", FlakyAction.TypeKey);
        step.RetryCount = 3;
        step.RetryDelaySeconds = 0;
        var flow = await host.AddFlowAsync([step]);

        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));
        var attempts = await host.GetStepsAsync(runId);
        Assert.Equal([1, 2, 3], attempts.Select(a => a.Attempt));
        Assert.Equal([FlowStepStatus.Failed, FlowStepStatus.Failed, FlowStepStatus.Succeeded], attempts.Select(a => a.Status));
    }

    [Fact]
    public async Task ExhaustedRetries_FailTheRun_AndSkipLaterSteps()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        host.Flaky.FailuresBeforeSuccess = 10;
        var step = Step("flaky", FlakyAction.TypeKey);
        step.RetryCount = 1;
        step.RetryDelaySeconds = 0;
        var flow = await host.AddFlowAsync([step, Set("never", "x", "1")]);

        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Failed, await host.RunAsync(runId));
        var run = await host.GetRunAsync(runId);
        Assert.Contains("flaky", run.Error);
        Assert.Equal(2, host.Flaky.Executions);
        Assert.DoesNotContain(await host.GetStepsAsync(runId), s => s.StepKey == "never");
    }

    [Fact]
    public async Task ContinueOnError_ExposesErrorPill_AndContinues()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var broken = Step("broken", SetVariableAction.TypeKey, ("name", "1nvalid"), ("value", "x"));
        broken.ContinueOnError = true;
        var flow = await host.AddFlowAsync([broken, Set("handler", "reason", "{{steps.broken.error}}")]);

        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));
        var context = FlowRunContext.Parse((await host.GetRunAsync(runId)).ContextJson);
        Assert.Contains("variable name", context.Variables["reason"]!.GetValue<string>());
    }

    [Fact]
    public async Task DisabledSteps_AreSkipped()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var skipped = Set("skipped", "x", "1");
        skipped.Disabled = true;
        var flow = await host.AddFlowAsync([skipped, Set("ran", "y", "2")]);

        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));
        Assert.Equal(["ran"], (await host.GetStepsAsync(runId)).Select(s => s.StepKey));
    }

    [Fact]
    public async Task UnknownAction_FailsTheRun()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync([Step("ghost", "does.not.exist")]);

        var runId = await host.StartAsync(flow.Id);

        Assert.Equal(FlowRunStatus.Failed, await host.RunAsync(runId));
        Assert.Contains("unknown action", (await host.GetRunAsync(runId)).Error);
    }

    [Fact]
    public async Task CancellingAWaitingRun_StopsIt()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync([Step("pause", WaitAction.TypeKey, ("minutes", "60")), Set("after", "x", "1")]);
        var runId = await host.StartAsync(flow.Id);
        Assert.Equal(FlowRunStatus.Waiting, await host.RunAsync(runId));

        var cancel = await host.WithAsync<IFlowRunService, Result>(s => s.CancelAsync(runId));

        Assert.True(cancel.Succeeded, cancel.Error);
        host.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(FlowRunStatus.Cancelled, await host.RunAsync(runId));
        Assert.DoesNotContain(await host.GetStepsAsync(runId), s => s.StepKey == "after");
    }

    [Fact]
    public async Task IdempotencyKey_PreventsDuplicateRuns()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync([Set("a", "x", "1")]);

        var first = await host.StartAsync(flow.Id, "event-42");
        var second = await host.StartAsync(flow.Id, "event-42");
        var third = await host.StartAsync(flow.Id, "event-43");

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
    }

    [Fact]
    public async Task InactiveFlows_OnlyStartAsTestRuns()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync([Set("a", "x", "1")], f => f.IsActive = false);

        var normal = await host.WithAsync<IFlowRunStarter, Result<Guid>>(s => s.StartAsync(new FlowStartRequest { FlowId = flow.Id, TriggerType = FlowTriggerType.Manual }));
        var test = await host.WithAsync<IFlowRunStarter, Result<Guid>>(s => s.StartAsync(new FlowStartRequest { FlowId = flow.Id, TriggerType = FlowTriggerType.Manual, IsTest = true }));

        Assert.Equal(ResultErrorKind.Validation, normal.ErrorKind);
        Assert.True(test.Succeeded, test.Error);
        Assert.True((await host.GetRunAsync(test.Value)).IsTest);
    }

    [Fact]
    public async Task RunUsesDefinitionSnapshot_FromStartTime()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync([Set("original", "x", "1")]);
        var runId = await host.StartAsync(flow.Id);

        await using (var db = host.Create())
        {
            var stored = await db.Set<Flow>().FindAsync(flow.Id);
            stored!.StepsJson = FlowJson.Serialize(new List<FlowStepDefinition> { Set("changed", "x", "2") });
            await db.SaveChangesAsync();
        }

        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(runId));
        Assert.Equal(["original"], (await host.GetStepsAsync(runId)).Select(s => s.StepKey));
    }

    [Fact]
    public async Task SensitiveValues_AreRedactedInSnapshots()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync([Set("secretstep", "password", "hunter2")]);
        var start = await host.WithAsync<IFlowRunStarter, Result<Guid>>(s => s.StartAsync(new FlowStartRequest
        {
            FlowId = flow.Id,
            TriggerType = FlowTriggerType.Webhook,
            Trigger = new JsonObject { ["body"] = new JsonObject { ["apiToken"] = "abc123", ["name"] = "visible" } },
        }));

        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(start.Value));
        var run = await host.GetRunAsync(start.Value);
        Assert.DoesNotContain("abc123", run.ContextJson);
        Assert.Contains("visible", run.ContextJson);
        Assert.DoesNotContain("hunter2", string.Concat((await host.GetStepsAsync(start.Value)).Select(s => s.OutputJson)));
    }

    [Fact]
    public async Task Validator_RejectsBrokenDefinitions_AndNormalizesKeys()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var model = new FlowDefinitionModel
        {
            Name = "Validation",
            TriggerType = FlowTriggerType.Schedule,
            CronExpression = "not a cron",
            Steps = [new FlowStepDefinition { Type = SetVariableAction.TypeKey }, new FlowStepDefinition { Type = SetVariableAction.TypeKey }, new FlowStepDefinition { Type = "nope" }],
        };

        var issues = await host.WithAsync<IFlowService, IReadOnlyList<string>>(s => Task.FromResult(s.Validate(model, forActivation: true)));

        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Contains("nope", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["set1", "set2"], model.Steps.Take(2).Select(s => s.Key));
    }
}
