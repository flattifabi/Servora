using Servora.Application.Activity;
using Servora.Application.BusinessRules;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Activity;
using Servora.Domain.BusinessRules;
using Servora.Domain.Common;

namespace Servora.Tests.Unit.Platform;

/// <summary>Tests for declarative business rules (before and after actions).</summary>
public sealed class BusinessRuleTests : IAsyncLifetime
{
    private const string Table = "u_work_item";
    private PlatformTestHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await PlatformTestHost.CreateAsync();
        await _host.RunAsync<ITableDesignerService>(async designer =>
        {
            Assert.True((await designer.CreateTableAsync(new CustomTableInput { Name = Table, Label = "Work item", NumberPrefix = "WRK" })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_title", Label = "Title", Type = FieldType.String })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_priority", Label = "Priority", Type = FieldType.Integer })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_category", Label = "Category", Type = FieldType.String })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_state", Label = "State", Type = FieldType.Choice, Choices = [new("open", "Open"), new("closed", "Closed")] })).Succeeded);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<Result<Guid>> SaveRuleAsync(BusinessRuleModel model) =>
        _host.RunAsync<IBusinessRuleService, Result<Guid>>(s => s.SaveAsync(model));

    private static ConditionGroup Rule(string field, ConditionOperator op, string? value = null) =>
        new() { Rules = [new ConditionRule { Field = field, Operator = op, Value = value }] };

    [Fact]
    public async Task BeforeRule_SetsFieldValue()
    {
        var saved = await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "Default category for high priority",
            TableName = Table,
            Condition = Rule("u_priority", ConditionOperator.LessOrEqual, "2"),
            Actions = [new BusinessRuleAction { Type = BusinessRuleActionType.SetField, Field = "u_category", Value = "urgent" }],
        });
        Assert.True(saved.Succeeded, saved.Error);

        var high = await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "A", ["u_priority"] = 1 }));
        var low = await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "B", ["u_priority"] = 4 }));

        var highRecord = await _host.RunAsync<IRecordService, Result<IReadOnlyDictionary<string, object?>>>(s => s.GetAsync(Table, high.Value));
        var lowRecord = await _host.RunAsync<IRecordService, Result<IReadOnlyDictionary<string, object?>>>(s => s.GetAsync(Table, low.Value));
        Assert.Equal("urgent", highRecord.Value!["u_category"]);
        Assert.Null(lowRecord.Value!["u_category"]);

        // The value set by the rule is indexed and therefore filterable.
        var urgent = await _host.RunAsync<IRecordService, Result<PagedResult<IReadOnlyDictionary<string, object?>>>>(s => s.QueryAsync(Table, new RecordQuery { Condition = Rule("u_category", ConditionOperator.Equals, "urgent") }));
        Assert.Equal(1, urgent.Value!.TotalCount);
    }

    [Fact]
    public async Task BeforeRule_AbortsWithMessage_UsingChangeOperators()
    {
        Assert.True((await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "Closed items are final",
            TableName = Table,
            OnInsert = false,
            OnUpdate = true,
            Condition = Rule("u_state", ConditionOperator.ChangedFrom, "closed"),
            Actions = [new BusinessRuleAction { Type = BusinessRuleActionType.Abort, Message = "${Number} is closed and cannot be reopened." }],
        })).Succeeded);

        var id = (await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "C", ["u_state"] = "closed" }))).Value;
        var renamed = await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(Table, id, new Dictionary<string, object?> { ["u_title"] = "C2" }));
        Assert.True(renamed.Succeeded, renamed.Error);

        var reopened = await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(Table, id, new Dictionary<string, object?> { ["u_state"] = "open" }));
        Assert.False(reopened.Succeeded);
        Assert.Equal(ResultErrorKind.Validation, reopened.ErrorKind);
        Assert.Equal("WRK0001001 is closed and cannot be reopened.", reopened.Error);

        var record = await _host.RunAsync<IRecordService, Result<IReadOnlyDictionary<string, object?>>>(s => s.GetAsync(Table, id));
        Assert.Equal("closed", record.Value!["u_state"]);
    }

    [Fact]
    public async Task AfterRule_AddsWorkNoteAndUpdatesRecord()
    {
        Assert.True((await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "Acknowledge",
            TableName = Table,
            Timing = BusinessRuleTiming.After,
            OnInsert = true,
            OnUpdate = false,
            Actions =
            [
                new BusinessRuleAction { Type = BusinessRuleActionType.AddWorkNote, Message = "Received ${u_title} (${u_state})" },
                new BusinessRuleAction { Type = BusinessRuleActionType.SetField, Field = "u_state", Value = "open" },
            ],
        })).Succeeded);

        var id = (await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "D" }))).Value;
        await _host.DispatchEventsAsync();

        var record = await _host.RunAsync<IRecordService, Result<IReadOnlyDictionary<string, object?>>>(s => s.GetAsync(Table, id));
        Assert.Equal("open", record.Value!["u_state"]);
        var notes = await _host.RunAsync<IActivityService, IReadOnlyList<ActivityEntry>>(a => a.GetAsync(Table, id, includeWorkNotes: true));
        var note = Assert.Single(notes, n => n.Type == ActivityType.WorkNote);
        Assert.Equal("Received D ()", note.Text);
        Assert.Equal("Business rule: Acknowledge", note.AuthorName);
    }

    [Fact]
    public async Task Validation_RejectsInvalidRules()
    {
        var abortAfter = await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "x", TableName = Table, Timing = BusinessRuleTiming.After,
            Actions = [new BusinessRuleAction { Type = BusinessRuleActionType.Abort, Message = "no" }],
        });
        Assert.Contains("before", abortAfter.Error);

        var readOnly = await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "x", TableName = Table,
            Actions = [new BusinessRuleAction { Type = BusinessRuleActionType.SetField, Field = "Number", Value = "1" }],
        });
        Assert.Contains("writable", readOnly.Error);

        var badValue = await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "x", TableName = Table,
            Actions = [new BusinessRuleAction { Type = BusinessRuleActionType.SetField, Field = "u_priority", Value = "high" }],
        });
        Assert.Contains("whole number", badValue.Error);

        var unknownField = await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "x", TableName = Table, Condition = Rule("Secret", ConditionOperator.IsEmpty),
            Actions = [new BusinessRuleAction { Type = BusinessRuleActionType.AddWorkNote, Message = "x" }],
        });
        Assert.Contains("Unknown field", unknownField.Error);

        var roleWrite = await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "x", TableName = TableNames.Role,
            Actions = [new BusinessRuleAction { Type = BusinessRuleActionType.SetField, Field = "IsSystem", Value = "false" }],
        });
        Assert.False(roleWrite.Succeeded);

        _host.Identity.Grant(Permissions.Platform.ReadRecords);
        Assert.Equal(ResultErrorKind.Forbidden, (await SaveRuleAsync(new BusinessRuleModel { Name = "x", TableName = Table })).ErrorKind);
    }

    [Fact]
    public async Task InactiveRules_DoNotRun()
    {
        var id = (await SaveRuleAsync(new BusinessRuleModel
        {
            Name = "Always abort",
            TableName = Table,
            Actions = [new BusinessRuleAction { Type = BusinessRuleActionType.Abort, Message = "Blocked" }],
        })).Value;
        Assert.False((await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "E" }))).Succeeded);

        Assert.True((await _host.RunAsync<IBusinessRuleService, Result>(s => s.SetActiveAsync(id, false))).Succeeded);
        Assert.True((await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "E" }))).Succeeded);
    }
}
