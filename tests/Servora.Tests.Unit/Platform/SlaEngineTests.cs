using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Platform;
using Servora.Application.Sla;
using Servora.Application.Tables;
using Servora.Domain.Activity;
using Servora.Domain.Sla;

namespace Servora.Tests.Unit.Platform;

/// <summary>End-to-end tests of the SLA engine on a custom task table.</summary>
public sealed class SlaEngineTests : IAsyncLifetime
{
    private const string Table = "u_ticket";
    private PlatformTestHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await PlatformTestHost.CreateAsync();
        await _host.RunAsync<ITableDesignerService>(async designer =>
        {
            Assert.True((await designer.CreateTableAsync(new CustomTableInput { Name = Table, Label = "Ticket", NumberPrefix = "TKT" })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput
            {
                Name = "u_state",
                Label = "State",
                Type = FieldType.Choice,
                Choices = [new("new", "New"), new("hold", "On hold"), new("resolved", "Resolved")],
                DefaultValue = "new",
            })).Succeeded);
        });

        var saved = await _host.RunAsync<ISlaAdminService, Result<Guid>>(s => s.SaveAsync(new SlaDefinitionModel
        {
            Name = "Resolution",
            TableName = Table,
            TargetMinutes = 60,
            PauseCondition = Rule("u_state", ConditionOperator.Equals, "hold"),
            StopCondition = Rule("u_state", ConditionOperator.Equals, "resolved"),
        }));
        Assert.True(saved.Succeeded, saved.Error);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static ConditionGroup Rule(string field, ConditionOperator op, string value) =>
        new() { Rules = [new ConditionRule { Field = field, Operator = op, Value = value }] };

    private async Task<Guid> CreateTicketAsync()
    {
        var id = await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?>()));
        Assert.True(id.Succeeded, id.Error);
        await _host.DispatchEventsAsync();
        return id.Value;
    }

    private async Task SetStateAsync(Guid id, string state)
    {
        Assert.True((await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(Table, id, new Dictionary<string, object?> { ["u_state"] = state }))).Succeeded);
        await _host.DispatchEventsAsync();
    }

    private Task<IReadOnlyList<TaskSlaInfo>> TimersAsync(Guid id) =>
        _host.RunAsync<ISlaService, IReadOnlyList<TaskSlaInfo>>(s => s.GetForRecordAsync(Table, id));

    [Fact]
    public async Task Timer_StartsAndBreaches()
    {
        var id = await CreateTicketAsync();
        var timer = Assert.Single(await TimersAsync(id));
        Assert.Equal("Resolution", timer.Name);
        Assert.Equal("InProgress", timer.Stage);
        Assert.NotNull(timer.DueAt);

        _host.Clock.Now = timer.DueAt!.Value.AddMinutes(1);
        var breached = await _host.RunAsync<SlaEngine, int>(e => e.EvaluateBreachesAsync(100, CancellationToken.None));
        Assert.Equal(1, breached);

        var after = Assert.Single(await TimersAsync(id));
        Assert.True(after.Breached);
        Assert.Equal("Breached", after.Stage);
        Assert.True(after.PercentElapsed > 100);

        var activity = await _host.RunAsync<IActivityService, IReadOnlyList<ActivityEntry>>(a => a.GetAsync(Table, id, includeWorkNotes: true));
        Assert.Contains(activity, a => a.Type == ActivityType.System && a.Text!.Contains("breached", StringComparison.Ordinal));
        Assert.Equal(0, await _host.RunAsync<SlaEngine, int>(e => e.EvaluateBreachesAsync(100, CancellationToken.None)));

        var other = await CreateTicketAsync();
        var bulk = await _host.RunAsync<ISlaService, IReadOnlySet<Guid>>(s => s.GetBreachedRecordIdsAsync(Table, [id, other, Guid.NewGuid()]));
        Assert.Equal([id], bulk);
    }

    [Fact]
    public async Task Timer_PausesResumesAndCompletes()
    {
        var id = await CreateTicketAsync();
        await SetStateAsync(id, "hold");
        Assert.Equal("Paused", Assert.Single(await TimersAsync(id)).Stage);

        await SetStateAsync(id, "new");
        Assert.Equal("InProgress", Assert.Single(await TimersAsync(id)).Stage);

        await SetStateAsync(id, "resolved");
        var done = Assert.Single(await TimersAsync(id));
        Assert.Equal("Achieved", done.Stage);
        Assert.False(done.Breached);

        // Further updates while the stop condition matches do not attach a new timer.
        await SetStateAsync(id, "resolved");
        Assert.Single(await TimersAsync(id));

        // Reopening attaches a new timer.
        await SetStateAsync(id, "new");
        Assert.Equal(2, (await TimersAsync(id)).Count);
    }

    [Fact]
    public async Task Timers_AreCancelledWhenRecordIsDeleted_AndHiddenWithoutReadAccess()
    {
        var id = await CreateTicketAsync();
        Assert.True((await _host.RunAsync<IRecordService, Result>(s => s.DeleteAsync(Table, id))).Succeeded);
        await _host.DispatchEventsAsync();
        Assert.Equal(SlaStage.Cancelled, await _host.DbAsync(db => db.Set<TaskSla>().Where(t => t.RecordId == id).Select(t => t.Stage).SingleAsync()));

        var other = await CreateTicketAsync();
        _host.Identity.Grant("platform.records.read");
        Assert.Empty(await TimersAsync(other));
    }

    [Fact]
    public async Task Overview_ReportsRunningTimers()
    {
        await CreateTicketAsync();
        var overview = await _host.RunAsync<ISlaAdminService, Result<SlaOverview>>(s => s.GetOverviewAsync());
        Assert.True(overview.Succeeded);
        Assert.Equal(1, overview.Value!.Running);
        Assert.StartsWith($"/table/{Table}/", Assert.Single(overview.Value.Timers).Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validation_RejectsInvalidDefinitions()
    {
        var result = await _host.RunAsync<ISlaAdminService, Result<Guid>>(s => s.SaveAsync(new SlaDefinitionModel
        {
            Name = "Bad",
            TableName = Table,
            TargetMinutes = 0,
        }));
        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);

        var hours = await _host.RunAsync<ISlaAdminService, Result<Guid>>(s => s.SaveAsync(new SlaDefinitionModel
        {
            Name = "Bad hours",
            TableName = Table,
            ScheduleType = SlaScheduleType.BusinessHours,
            DayStartMinute = 600,
            DayEndMinute = 500,
        }));
        Assert.Contains("end after", hours.Error);
    }
}
