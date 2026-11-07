using System.Text;
using Servora.Application.Automation;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Services;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Automation;
using static Servora.Tests.Unit.Automation.TestSteps;

namespace Servora.Tests.Unit.Automation;

/// <summary>Flow Designer use cases (create, version, activate, test, webhook, permissions) and the execution contract.</summary>
public sealed class FlowServiceTests
{
    private static async Task<Guid> CreateAsync(AutomationTestHost host, FlowTriggerType trigger = FlowTriggerType.Manual, string name = "My flow", string? table = null)
    {
        var created = await host.WithAsync<IFlowService, Result<Guid>>(s => s.CreateAsync(new FlowCreateRequest(name, "Test", trigger, table)));
        Assert.True(created.Succeeded, created.Error);
        return created.Value;
    }

    [Fact]
    public async Task Create_Save_Activate_IncrementsVersion()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var id = await CreateAsync(host, FlowTriggerType.RecordCreated, table: "incident");
        var model = (await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.GetAsync(id))).Value!;
        Assert.False(model.IsActive);
        Assert.Equal(1, model.DefinitionVersion);

        model.Steps.Add(new FlowStepDefinition { Type = LogAction.TypeKey, Config = { ["message"] = "Created {{trigger.record.Number}}" } });
        var saved = await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.SaveAsync(model, "Log new incidents"));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(2, saved.Value!.DefinitionVersion);
        Assert.Equal("Log new incidents", saved.Value.VersionNote);
        Assert.Equal("log1", saved.Value.Steps.Single().Key);

        var stale = await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.SaveAsync(model, "stale"));
        Assert.Equal(ResultErrorKind.Conflict, stale.ErrorKind);

        Assert.True((await host.WithAsync<IFlowService, Result>(s => s.SetActiveAsync(id, true))).Succeeded);
        Assert.True((await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.GetAsync(id))).Value!.IsActive);
        Assert.Contains(host.Audit.Entries, e => e.Action == "flow.activated");
    }

    [Fact]
    public async Task Activation_IsRefusedForIncompleteFlows()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var recordFlow = await CreateAsync(host, FlowTriggerType.RecordUpdated, "No table");
        var webhookFlow = await CreateAsync(host, FlowTriggerType.Webhook, "No secret");

        var missingTable = await host.WithAsync<IFlowService, Result>(s => s.SetActiveAsync(recordFlow, true));
        var missingSecret = await host.WithAsync<IFlowService, Result>(s => s.SetActiveAsync(webhookFlow, true));

        Assert.Equal(ResultErrorKind.Validation, missingTable.ErrorKind);
        Assert.Contains("secret", missingSecret.Error);
    }

    [Fact]
    public async Task ScheduledFlow_GetsNextRunOnActivation()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var id = await CreateAsync(host, FlowTriggerType.Schedule, "Weekday report");

        Assert.True((await host.WithAsync<IFlowService, Result>(s => s.SetActiveAsync(id, true))).Succeeded);

        var model = (await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.GetAsync(id))).Value!;
        Assert.Equal("0 8 * * 1-5", model.CronExpression);

        // 2026-11-09 08:00 UTC is a Monday at 08:00 sharp, so the next run is Tuesday.
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero), model.NextRunAt);
    }

    [Fact]
    public async Task TestRun_WorksForInactiveFlows_AndValidatesInputs()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var id = await CreateAsync(host);
        var model = (await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.GetAsync(id))).Value!;
        model.Inputs.Add(new FlowInputDefinition { Name = "count", Label = "Count", Type = "number", Required = true });
        model.Steps.Add(Set("double", "result", "{{inputs.count}}"));
        Assert.True((await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.SaveAsync(model, null))).Succeeded);

        var missing = await host.WithAsync<IFlowService, Result<Guid>>(s => s.StartTestRunAsync(id, new FlowTestRequest()));
        var notNumber = await host.WithAsync<IFlowService, Result<Guid>>(s => s.StartTestRunAsync(id, new FlowTestRequest { Inputs = new Dictionary<string, string> { ["count"] = "abc" } }));
        var ok = await host.WithAsync<IFlowService, Result<Guid>>(s => s.StartTestRunAsync(id, new FlowTestRequest { Inputs = new Dictionary<string, string> { ["count"] = "21" } }));

        Assert.Equal(ResultErrorKind.Validation, missing.ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, notNumber.ErrorKind);
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(ok.Value));
        var detail = await host.WithAsync<IFlowRunService, Result<FlowRunDetail>>(s => s.GetAsync(ok.Value));
        Assert.True(detail.Value!.Run.IsTest);
        Assert.Single(detail.Value.Steps);
        Assert.Contains("21", detail.Value.ContextJson);
    }

    [Fact]
    public async Task Webhook_RequiresSecretOrValidSignature()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var id = await CreateAsync(host, FlowTriggerType.Webhook, "Inbound");
        var secret = (await host.WithAsync<IFlowService, Result<string>>(s => s.RotateWebhookSecretAsync(id))).Value!;
        var model = (await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.GetAsync(id))).Value!;
        model.Steps.Add(Set("store", "ticket", "{{trigger.body.ticket}}"));
        Assert.True((await host.WithAsync<IFlowService, Result<FlowDefinitionModel>>(s => s.SaveAsync(model, null))).Succeeded);
        Assert.True((await host.WithAsync<IFlowService, Result>(s => s.SetActiveAsync(id, true))).Succeeded);
        var body = Encoding.UTF8.GetBytes("""{ "ticket": "EXT-77" }""");

        var noSecret = await host.WithAsync<FlowWebhookService, Result<Guid>>(s => s.ReceiveAsync(id, null, null, body, "application/json", null));
        var wrongSecret = await host.WithAsync<FlowWebhookService, Result<Guid>>(s => s.ReceiveAsync(id, "guess", null, body, "application/json", null));
        var badSignature = await host.WithAsync<FlowWebhookService, Result<Guid>>(s => s.ReceiveAsync(id, null, "sha256=00", body, "application/json", null));
        var bySecret = await host.WithAsync<FlowWebhookService, Result<Guid>>(s => s.ReceiveAsync(id, secret, null, body, "application/json", "delivery-1"));
        var bySecretAgain = await host.WithAsync<FlowWebhookService, Result<Guid>>(s => s.ReceiveAsync(id, secret, null, body, "application/json", "delivery-1"));
        var bySignature = await host.WithAsync<FlowWebhookService, Result<Guid>>(s => s.ReceiveAsync(id, null, FlowWebhookService.ComputeSignature(secret, body), body, "application/json", null));
        var unknownFlow = await host.WithAsync<FlowWebhookService, Result<Guid>>(s => s.ReceiveAsync(Guid.CreateVersion7(), secret, null, body, null, null));

        Assert.Equal(ResultErrorKind.Forbidden, noSecret.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, wrongSecret.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, badSignature.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, unknownFlow.ErrorKind);
        Assert.True(bySecret.Succeeded, bySecret.Error);
        Assert.Equal(bySecret.Value, bySecretAgain.Value);
        Assert.True(bySignature.Succeeded, bySignature.Error);
        Assert.Equal(FlowRunStatus.Succeeded, await host.RunAsync(bySecret.Value));
        Assert.Contains("EXT-77", (await host.GetRunAsync(bySecret.Value)).ContextJson);
    }

    [Fact]
    public async Task ExecutionContract_ChecksPermissionAndStartsActiveFlows()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var flow = await host.AddFlowAsync([Set("a", "x", "{{inputs.name}}")], f =>
        {
            f.Name = "Greet";
            f.InputsJson = FlowJson.Serialize(new List<FlowInputDefinition> { new() { Name = "name", Required = true } });
        });

        var startable = await host.WithAsync<IFlowExecutionService, IReadOnlyList<FlowSummary>>(s => s.ListStartableAsync());
        var started = await host.WithAsync<IFlowExecutionService, Result<Guid>>(s => s.StartAsync(flow.Id, new Dictionary<string, object?> { ["name"] = "Ada" }));
        var missingInput = await host.WithAsync<IFlowExecutionService, Result<Guid>>(s => s.StartAsync(flow.Id, new Dictionary<string, object?>()));
        Assert.Contains(startable, f => f.Id == flow.Id);
        Assert.True(started.Succeeded, started.Error);
        Assert.Equal(ResultErrorKind.Validation, missingInput.ErrorKind);

        await host.RunAsync(started.Value);
        var summary = await host.WithAsync<IFlowExecutionService, Result<FlowRunSummary>>(s => s.GetRunAsync(started.Value));
        Assert.Equal("Succeeded", summary.Value!.Status);

        host.User.Grant(Permissions.Automation.Read);
        var forbidden = await host.WithAsync<IFlowExecutionService, Result<Guid>>(s => s.StartAsync(flow.Id, new Dictionary<string, object?> { ["name"] = "Ada" }));
        Assert.Equal(ResultErrorKind.Forbidden, forbidden.ErrorKind);
    }

    [Fact]
    public async Task ManagementMethods_RequirePermissions()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var id = await CreateAsync(host);
        host.User.Grant(Permissions.Automation.Read);

        Assert.True((await host.WithAsync<IFlowService, Result<PagedResult<FlowListItem>>>(s => s.ListAsync(new FlowListQuery()))).Succeeded);
        Assert.Equal(ResultErrorKind.Forbidden, (await host.WithAsync<IFlowService, Result<Guid>>(s => s.CreateAsync(new FlowCreateRequest("x", null, FlowTriggerType.Manual, null)))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await host.WithAsync<IFlowService, Result>(s => s.SetActiveAsync(id, true))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await host.WithAsync<IFlowService, Result>(s => s.DeleteAsync(id))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await host.WithAsync<IFlowService, Result<string>>(s => s.RotateWebhookSecretAsync(id))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await host.WithAsync<IFlowService, Result<Guid>>(s => s.StartTestRunAsync(id, new FlowTestRequest()))).ErrorKind);

        host.User.Grant();
        Assert.Equal(ResultErrorKind.Forbidden, (await host.WithAsync<IFlowService, Result<PagedResult<FlowListItem>>>(s => s.ListAsync(new FlowListQuery()))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await host.WithAsync<IFlowRunService, Result<PagedResult<FlowRunListItem>>>(s => s.ListAsync(new FlowRunQuery()))).ErrorKind);
    }

    [Fact]
    public async Task DataPills_IncludeTriggerFieldsAndPrecedingStepsOnly()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var lookup = Step("lookup", LookupRecordsAction.TypeKey, ("table", "incident"));
        var target = Set("target", "x", "1");
        var later = Set("later", "y", "2");
        var model = new FlowDefinitionModel
        {
            TriggerType = FlowTriggerType.RecordUpdated,
            TriggerTable = "incident",
            Steps = [lookup, If("check", new FlowCondition(), [target], [Set("other", "z", "3")]), later],
        };
        var descriptors = await host.WithAsync<IFlowService, IReadOnlyList<FlowActionDescriptor>>(s => Task.FromResult(s.GetActions()));

        var pills = FlowDataPillBuilder.Build(model, target.Id, descriptors, name => name == "incident" ? Fakes.FakeTableRegistry.Incident : null);

        var paths = pills.Select(p => p.Path).ToList();
        Assert.Contains("trigger.record.ShortDescription", paths);
        Assert.Contains("steps.lookup.count", paths);
        Assert.Contains("steps.lookup.first.Priority", paths);
        Assert.Contains("steps.check.result", paths);
        Assert.DoesNotContain(paths, p => p.StartsWith("steps.later", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, p => p.StartsWith("steps.other", StringComparison.Ordinal));
        Assert.DoesNotContain("vars.y", paths);
    }
}
