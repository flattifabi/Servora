using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Platform;

namespace Servora.Tests.Unit.Platform;

/// <summary>Tests for the generic record service on custom and typed tables (validation, permissions, index, display values).</summary>
public sealed class RecordServiceTests : IAsyncLifetime
{
    private const string Table = "u_asset_audit";
    private PlatformTestHost _host = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _host = await PlatformTestHost.CreateAsync();
        _userId = await _host.DbAsync(async db =>
        {
            var user = new User { UserName = "alice", NormalizedUserName = "ALICE", DisplayName = "Alice Auditor", Email = "alice@example.com", PasswordHash = "AQAAAAE-secret" };
            db.Set<User>().Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });

        await _host.RunAsync<ITableDesignerService>(async designer =>
        {
            var created = await designer.CreateTableAsync(new CustomTableInput { Name = Table, Label = "Asset audit", NumberPrefix = "AUD", Icon = "clipboard-list" });
            Assert.True(created.Succeeded, created.Error);
            await AddField(designer, new CustomFieldInput { Name = "u_title", Label = "Title", Type = FieldType.String, IsRequired = true, MaxLength = 40 });
            await AddField(designer, new CustomFieldInput { Name = "u_count", Label = "Count", Type = FieldType.Integer, DefaultValue = "1" });
            await AddField(designer, new CustomFieldInput { Name = "u_state", Label = "State", Type = FieldType.Choice, Choices = [new("new", "New"), new("done", "Done")], DefaultValue = "new" });
            await AddField(designer, new CustomFieldInput { Name = "u_auditor", Label = "Auditor", Type = FieldType.Reference, ReferenceTable = TableNames.User });
            await AddField(designer, new CustomFieldInput { Name = "u_due", Label = "Due", Type = FieldType.Date });
            await AddField(designer, new CustomFieldInput { Name = "u_link", Label = "Link", Type = FieldType.Url });
            await AddField(designer, new CustomFieldInput { Name = "u_flag", Label = "Flag", Type = FieldType.Boolean });
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static async Task AddField(ITableDesignerService designer, CustomFieldInput input)
    {
        var result = await designer.AddFieldAsync(Table, input);
        Assert.True(result.Succeeded, result.Error);
    }

    private Task<Result<Guid>> CreateAsync(Dictionary<string, object?> values) =>
        _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, values));

    private Task<Result<PagedResult<IReadOnlyDictionary<string, object?>>>> QueryAsync(RecordQuery query) =>
        _host.RunAsync<IRecordService, Result<PagedResult<IReadOnlyDictionary<string, object?>>>>(s => s.QueryAsync(Table, query));

    [Fact]
    public async Task Registry_MergesCustomTableAndFields()
    {
        var table = await _host.RunAsync<ITableRegistry, TableDefinition?>(r => r.GetTableAsync(Table));
        Assert.NotNull(table);
        Assert.True(table.IsCustom);
        Assert.Equal("Number", table.DisplayField);
        Assert.Equal(FieldType.Choice, table.FindField("u_state")!.Type);
        Assert.Equal(2, table.FindField("u_state")!.Choices.Count);
        Assert.True(table.FindField("Number")!.IsReadOnly);
    }

    [Fact]
    public async Task Create_AppliesDefaultsNumberAndDisplayValues()
    {
        var created = await CreateAsync(new() { ["u_title"] = "Laptop audit", ["u_auditor"] = _userId.ToString() });
        Assert.True(created.Succeeded, created.Error);

        var record = await _host.RunAsync<IRecordService, Result<IReadOnlyDictionary<string, object?>>>(s => s.GetAsync(Table, created.Value));
        Assert.True(record.Succeeded);
        Assert.Equal("AUD0001001", record.Value!["Number"]);
        Assert.Equal(1L, record.Value["u_count"]);
        Assert.Equal("new", record.Value["u_state"]);
        Assert.Equal("New", record.Value["u_state.display"]);
        Assert.Equal(_userId, record.Value["u_auditor"]);
        Assert.Equal("Alice Auditor", record.Value["u_auditor.display"]);
    }

    [Theory]
    [InlineData("u_title", null, "mandatory")]
    [InlineData("u_unknown", "x", "Unknown field")]
    [InlineData("Number", "AUD1", "read-only")]
    [InlineData("u_count", "abc", "whole number")]
    [InlineData("u_count", "1.5", "whole number")]
    [InlineData("u_state", "invalid", "must be one of")]
    [InlineData("u_due", "tomorrow", "date")]
    [InlineData("u_link", "javascript:alert(1)", "http")]
    [InlineData("u_flag", "maybe", "true or false")]
    [InlineData("u_auditor", "not-a-guid", "record identifier")]
    public async Task Create_ValidatesValues(string field, string? value, string expectedError)
    {
        var values = new Dictionary<string, object?> { ["u_title"] = "Valid" };
        values[field] = value;
        var result = await CreateAsync(values);
        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
        Assert.Contains(expectedError, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RejectsTooLongTextAndDanglingReferences()
    {
        var tooLong = await CreateAsync(new() { ["u_title"] = new string('x', 41) });
        Assert.Contains("40", tooLong.Error);

        var dangling = await CreateAsync(new() { ["u_title"] = "x", ["u_auditor"] = Guid.NewGuid().ToString() });
        Assert.Contains("does not exist", dangling.Error);
    }

    [Fact]
    public async Task Query_FiltersSortsAndSearchesCustomFieldsInDatabase()
    {
        await CreateAsync(new() { ["u_title"] = "Alpha", ["u_count"] = 5, ["u_state"] = "done", ["u_due"] = "2026-05-01" });
        await CreateAsync(new() { ["u_title"] = "Beta", ["u_count"] = 12, ["u_flag"] = true });
        await CreateAsync(new() { ["u_title"] = "Gamma", ["u_count"] = 8, ["u_auditor"] = _userId });

        var greater = await QueryAsync(new RecordQuery
        {
            Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_count", Operator = ConditionOperator.GreaterThan, Value = "6" }] },
            Page = new PageRequest { SortBy = "u_count", SortDescending = true },
        });
        Assert.True(greater.Succeeded, greater.Error);
        Assert.Equal(["Beta", "Gamma"], greater.Value!.Items.Select(r => (string)r["u_title"]!).ToList());

        var done = await QueryAsync(new RecordQuery { Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_state", Operator = ConditionOperator.Equals, Value = "DONE" }] } });
        Assert.Equal("Alpha", Assert.Single(done.Value!.Items)["u_title"]);

        var flagged = await QueryAsync(new RecordQuery { Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_flag", Operator = ConditionOperator.Equals, Value = "yes" }] } });
        Assert.Equal("Beta", Assert.Single(flagged.Value!.Items)["u_title"]);

        var withDue = await QueryAsync(new RecordQuery { Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_due", Operator = ConditionOperator.LessThan, Value = "2026-06-01" }] } });
        Assert.Equal("Alpha", Assert.Single(withDue.Value!.Items)["u_title"]);

        var unassigned = await QueryAsync(new RecordQuery { Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_auditor", Operator = ConditionOperator.IsEmpty }] } });
        Assert.Equal(2, unassigned.Value!.TotalCount);

        var mine = await _host.RunAsync<IRecordService, Result<PagedResult<IReadOnlyDictionary<string, object?>>>>(s => s.QueryAsync(Table, new RecordQuery
        {
            Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_auditor", Operator = ConditionOperator.Equals, Value = _userId.ToString() }] },
        }));
        Assert.Equal("Gamma", Assert.Single(mine.Value!.Items)["u_title"]);

        var search = await QueryAsync(new RecordQuery { Page = new PageRequest { Search = "amm" } });
        Assert.Equal("Gamma", Assert.Single(search.Value!.Items)["u_title"]);

        var paged = await QueryAsync(new RecordQuery { Page = new PageRequest { PageSize = 2, Page = 2, SortBy = "u_title" }, Fields = ["u_title"] });
        Assert.Equal(3, paged.Value!.TotalCount);
        var last = Assert.Single(paged.Value.Items);
        Assert.Equal("Gamma", last["u_title"]);
        Assert.False(last.ContainsKey("u_count"));
        Assert.True(last.ContainsKey("Id"));
    }

    [Fact]
    public async Task Query_RejectsUnknownFieldsInConditionsSortAndProjection()
    {
        var condition = await QueryAsync(new RecordQuery { Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "DataJson", Operator = ConditionOperator.Contains, Value = "x" }] } });
        Assert.Equal(ResultErrorKind.Validation, condition.ErrorKind);

        var sort = await QueryAsync(new RecordQuery { Page = new PageRequest { SortBy = "TableName" } });
        Assert.Equal(ResultErrorKind.Validation, sort.ErrorKind);

        var fields = await QueryAsync(new RecordQuery { Fields = ["DataJson"] });
        Assert.Equal(ResultErrorKind.Validation, fields.ErrorKind);
    }

    [Fact]
    public async Task UpdateAndDelete_MaintainIndex()
    {
        var id = (await CreateAsync(new() { ["u_title"] = "Delta", ["u_count"] = 3 })).Value;
        var updated = await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(Table, id, new Dictionary<string, object?> { ["u_count"] = "30", ["u_title"] = "Delta 2" }));
        Assert.True(updated.Succeeded, updated.Error);

        var found = await QueryAsync(new RecordQuery { Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_count", Operator = ConditionOperator.Equals, Value = "30" }] } });
        Assert.Equal("Delta 2", Assert.Single(found.Value!.Items)["u_title"]);

        var cleared = await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(Table, id, new Dictionary<string, object?> { ["u_title"] = "" }));
        Assert.Contains("mandatory", cleared.Error);

        var deleted = await _host.RunAsync<IRecordService, Result>(s => s.DeleteAsync(Table, id));
        Assert.True(deleted.Succeeded);
        Assert.Equal(0, await _host.DbAsync(db => db.Set<RecordFieldValue>().CountAsync(v => v.RecordId == id)));
        Assert.Equal(ResultErrorKind.NotFound, (await _host.RunAsync<IRecordService, Result<IReadOnlyDictionary<string, object?>>>(s => s.GetAsync(Table, id))).ErrorKind);
    }

    [Fact]
    public async Task CustomTables_RequireTablePermissions()
    {
        _host.Identity.Grant(Permissions.Platform.ReadRecords);
        Assert.Equal(ResultErrorKind.Forbidden, (await QueryAsync(new RecordQuery())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await CreateAsync(new() { ["u_title"] = "x" })).ErrorKind);

        _host.Identity.Grant($"table.{Table}.read");
        Assert.True((await QueryAsync(new RecordQuery())).Succeeded);
        Assert.Equal(ResultErrorKind.Forbidden, (await CreateAsync(new() { ["u_title"] = "x" })).ErrorKind);

        _host.Identity.Grant($"table.{Table}.*");
        Assert.True((await CreateAsync(new() { ["u_title"] = "x" })).Succeeded);
    }

    [Fact]
    public async Task TypedTables_MapModulePermissionsAndHideSecrets()
    {
        _host.Identity.Grant(Permissions.Identity.ReadUsers);
        var users = await _host.RunAsync<IRecordService, Result<PagedResult<IReadOnlyDictionary<string, object?>>>>(s => s.QueryAsync(TableNames.User, new RecordQuery()));
        Assert.True(users.Succeeded, users.Error);
        var alice = users.Value!.Items.Single(u => (string?)u["UserName"] == "alice");
        Assert.False(alice.ContainsKey("PasswordHash"));
        Assert.False(alice.ContainsKey("SecurityStamp"));
        Assert.False(alice.ContainsKey("NormalizedUserName"));

        var filterOnSecret = await _host.RunAsync<IRecordService, Result<PagedResult<IReadOnlyDictionary<string, object?>>>>(s => s.QueryAsync(TableNames.User, new RecordQuery
        {
            Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "PasswordHash", Operator = ConditionOperator.StartsWith, Value = "AQ" }] },
        }));
        Assert.Equal(ResultErrorKind.Validation, filterOnSecret.ErrorKind);

        Assert.Equal(ResultErrorKind.Forbidden, (await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(TableNames.User, _userId, new Dictionary<string, object?> { ["Title"] = "x" }))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(TableNames.User, new Dictionary<string, object?> { ["UserName"] = "x" }))).ErrorKind);

        _host.Identity.Grant(Permissions.Identity.ReadUsers, Permissions.Identity.ManageUsers);
        var secret = await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(TableNames.User, _userId, new Dictionary<string, object?> { ["PasswordHash"] = "x" }));
        Assert.Contains("Unknown field", secret.Error);
        var security = await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(TableNames.User, _userId, new Dictionary<string, object?> { ["MustChangePassword"] = false }));
        Assert.Contains("read-only", security.Error);
        var profile = await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(TableNames.User, _userId, new Dictionary<string, object?> { ["Title"] = "Lead auditor" }));
        Assert.True(profile.Succeeded, profile.Error);

        // Users can never be created or deleted generically, even with the generic table keys.
        _host.Identity.Grant("table.*");
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.RunAsync<IRecordService, Result>(s => s.DeleteAsync(TableNames.User, _userId))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(TableNames.Role, new Dictionary<string, object?> { ["Name"] = "x", ["Label"] = "x" }))).ErrorKind);
    }

    [Fact]
    public async Task TypedTables_SupportCustomFieldsAndCrud()
    {
        var field = await _host.RunAsync<ITableDesignerService, Result<string>>(d => d.AddFieldAsync(TableNames.ConfigurationItem, new CustomFieldInput { Name = "u_rack", Label = "Rack", Type = FieldType.String }));
        Assert.True(field.Succeeded, field.Error);

        var id = await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(TableNames.ConfigurationItem, new Dictionary<string, object?>
        {
            ["Name"] = "SRV-42", ["ClassName"] = "server", ["OperationalStatus"] = "Maintenance", ["Criticality"] = 2, ["u_rack"] = "R12", ["OwnedById"] = _userId,
        }));
        Assert.True(id.Succeeded, id.Error);

        var byRack = await _host.RunAsync<IRecordService, Result<PagedResult<IReadOnlyDictionary<string, object?>>>>(s => s.QueryAsync(TableNames.ConfigurationItem, new RecordQuery
        {
            Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_rack", Operator = ConditionOperator.Equals, Value = "r12" }] },
        }));
        var row = Assert.Single(byRack.Value!.Items);
        Assert.Equal("SRV-42", row["Name"]);
        Assert.Equal("Maintenance", row["OperationalStatus.display"]);
        Assert.Equal("Alice Auditor", row["OwnedById.display"]);

        var unsupported = await _host.RunAsync<ITableDesignerService, Result<string>>(d => d.AddFieldAsync(TableNames.Role, new CustomFieldInput { Name = "u_x", Label = "X" }));
        Assert.False(unsupported.Succeeded);
    }

    [Fact]
    public async Task Designer_ValidatesNamesAndRequiresPermission()
    {
        var invalid = await _host.RunAsync<ITableDesignerService, Result<string>>(d => d.CreateTableAsync(new CustomTableInput { Name = "incident", Label = "Bad" }));
        Assert.Equal(ResultErrorKind.Validation, invalid.ErrorKind);

        var duplicate = await _host.RunAsync<ITableDesignerService, Result<string>>(d => d.CreateTableAsync(new CustomTableInput { Name = Table, Label = "Dup" }));
        Assert.Equal(ResultErrorKind.Conflict, duplicate.ErrorKind);

        var reserved = await _host.RunAsync<ITableDesignerService, Result<string>>(d => d.CreateTableAsync(new CustomTableInput { Name = "u_other", Label = "Other", NumberPrefix = "INC" }));
        Assert.Contains("reserved", reserved.Error);

        _host.Identity.Grant(Permissions.Platform.ReadRecords);
        var forbidden = await _host.RunAsync<ITableDesignerService, Result<string>>(d => d.CreateTableAsync(new CustomTableInput { Name = "u_other", Label = "Other" }));
        Assert.Equal(ResultErrorKind.Forbidden, forbidden.ErrorKind);
    }

    [Fact]
    public async Task NumberResolver_FindsCustomRecords()
    {
        var id = (await CreateAsync(new() { ["u_title"] = "Resolve me" })).Value;
        var number = (string)(await _host.RunAsync<IRecordService, Result<IReadOnlyDictionary<string, object?>>>(s => s.GetAsync(Table, id))).Value!["Number"]!;

        var location = await _host.RunAsync<IRecordNumberResolver, Result<RecordLocation>>(r => r.ResolveAsync(number.ToLowerInvariant()));
        Assert.True(location.Succeeded, location.Error);
        Assert.Equal($"/table/{Table}/{id}", location.Value!.Url);

        Assert.Equal(ResultErrorKind.NotFound, (await _host.RunAsync<IRecordNumberResolver, Result<RecordLocation>>(r => r.ResolveAsync("AUD9999999"))).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await _host.RunAsync<IRecordNumberResolver, Result<RecordLocation>>(r => r.ResolveAsync("../etc"))).ErrorKind);
    }
}
