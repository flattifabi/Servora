using Servora.Application.Common;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Application.Tables;

namespace Servora.Tests.Unit.Platform;

/// <summary>Tests for form layouts and list columns of the studio and the record URL builder.</summary>
public sealed class TableLayoutTests : IAsyncLifetime
{
    private const string Table = "u_vendor";
    private PlatformTestHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await PlatformTestHost.CreateAsync();
        await _host.RunAsync<ITableDesignerService>(async designer =>
        {
            Assert.True((await designer.CreateTableAsync(new CustomTableInput { Name = Table, Label = "Vendor" })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_name", Label = "Name", Type = FieldType.String, IsRequired = true })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_notes", Label = "Notes", Type = FieldType.Text })).Succeeded);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<Result<TableLayoutModel>> GetLayoutAsync() =>
        _host.RunAsync<ITableDesignerService, Result<TableLayoutModel>>(d => d.GetLayoutAsync(Table));

    private Task<Result> SaveLayoutAsync(TableLayoutModel layout) =>
        _host.RunAsync<ITableDesignerService, Result>(d => d.SaveLayoutAsync(Table, layout));

    [Fact]
    public async Task DefaultLayout_ContainsCustomFieldsAndNoSystemFields()
    {
        var layout = await GetLayoutAsync();
        Assert.True(layout.Succeeded, layout.Error);
        Assert.False(layout.Value!.IsCustomized);
        var section = Assert.Single(layout.Value.Sections);
        Assert.Equal(["u_name", "u_notes"], section.Fields);
        Assert.NotEmpty(layout.Value.ListColumns);
    }

    [Fact]
    public async Task SavedLayout_IsReturnedAndNewFieldsAreNeverHidden()
    {
        var saved = await SaveLayoutAsync(new TableLayoutModel
        {
            Sections = [new FormSectionModel { Title = "Vendor", Fields = ["U_NAME"] }],
            ListColumns = ["u_name", "UpdatedAt"],
        });
        Assert.True(saved.Succeeded, saved.Error);

        await _host.RunAsync<ITableDesignerService>(async d =>
            Assert.True((await d.AddFieldAsync(Table, new CustomFieldInput { Name = "u_rating", Label = "Rating", Type = FieldType.Integer })).Succeeded));

        var layout = (await GetLayoutAsync()).Value!;
        Assert.True(layout.IsCustomized);
        Assert.Equal("Vendor", layout.Sections[0].Title);
        Assert.Equal(["u_name"], layout.Sections[0].Fields);
        Assert.Contains(layout.Sections, s => s.Fields.Contains("u_notes") || s.Fields.Contains("u_rating"));
        Assert.Contains(layout.Sections.SelectMany(s => s.Fields), f => f == "u_rating");
        Assert.Equal(["u_name", "UpdatedAt"], layout.ListColumns);
    }

    [Fact]
    public async Task SaveLayout_ValidatesFieldsTitlesAndColumns()
    {
        var unknown = await SaveLayoutAsync(new TableLayoutModel { Sections = [new FormSectionModel { Title = "A", Fields = ["u_missing"] }], ListColumns = ["u_name"] });
        Assert.Equal(ResultErrorKind.Validation, unknown.ErrorKind);

        var untitled = await SaveLayoutAsync(new TableLayoutModel { Sections = [new FormSectionModel { Title = " ", Fields = ["u_name"] }], ListColumns = ["u_name"] });
        Assert.Equal(ResultErrorKind.Validation, untitled.ErrorKind);

        var noColumns = await SaveLayoutAsync(new TableLayoutModel { Sections = [new FormSectionModel { Title = "A", Fields = ["u_name"] }] });
        Assert.Equal(ResultErrorKind.Validation, noColumns.ErrorKind);
    }

    [Fact]
    public async Task Layout_RequiresPermissions()
    {
        _host.Identity.Grant(Permissions.Platform.ReadRecords);
        Assert.Equal(ResultErrorKind.Forbidden, (await GetLayoutAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await SaveLayoutAsync(new TableLayoutModel { ListColumns = ["u_name"] })).ErrorKind);

        _host.Identity.Grant(Permissions.Platform.ReadRecords, TablePolicies.GenericPermission(Table, TableOperation.Read));
        Assert.True((await GetLayoutAsync()).Succeeded);
        Assert.Equal(ResultErrorKind.Forbidden, (await SaveLayoutAsync(new TableLayoutModel { ListColumns = ["u_name"] })).ErrorKind);
    }

    [Fact]
    public void RecordUrls_UseModuleRoutesForTypedTablesAndGenericFormOtherwise()
    {
        var id = Guid.Parse("5f0b6a3e-1f43-4a5e-9d4c-2c1f0e7d9a10");
        var typed = new TableDefinition { Name = "incident", Label = "Incident", PluralLabel = "Incidents", DisplayField = "Number", Route = "/incidents/" };
        var custom = new TableDefinition { Name = "u_vendor", Label = "Vendor", PluralLabel = "Vendors", DisplayField = "Id", Route = "/table/u_vendor", IsCustom = true };
        var noRoute = typed with { Route = null };

        Assert.Equal($"/incidents/{id}", RecordUrls.For(typed, id));
        Assert.Equal($"/table/u_vendor/{id}", RecordUrls.For(custom, id));
        Assert.Equal($"/table/incident/{id}", RecordUrls.For(noRoute, id));

        Assert.Equal($"https://servora.example.com/incidents/{id}", RecordUrls.Absolute("https://servora.example.com", $"/incidents/{id}"));
        Assert.Equal("/incidents/1", RecordUrls.Absolute("javascript:alert(1)", "/incidents/1"));
        Assert.Equal("/incidents/1", RecordUrls.Absolute(null, "/incidents/1"));
    }
}
