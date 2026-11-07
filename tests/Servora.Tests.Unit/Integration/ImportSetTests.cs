using System.Text;
using Servora.Application.Common;
using Servora.Application.Integration.Imports;
using Servora.Application.Security;
using Servora.Domain.Integration;
using Servora.Tests.Unit.Automation;

namespace Servora.Tests.Unit.Integration;

/// <summary>Import sets: CSV parsing, transform maps with coalesce (insert / update), data source imports and permissions.</summary>
public sealed class ImportSetTests
{
    private static ImportSetModel ContactImport(ImportSourceType source = ImportSourceType.CsvUpload) => new()
    {
        Name = $"Contacts {Guid.NewGuid():N}"[..17],
        SourceType = source,
        TargetTable = "contact",
        CoalesceField = "Email",
        FieldMaps =
        [
            new ImportFieldMap { SourcePath = "email", TargetField = "Email", Transform = "lower" },
            new ImportFieldMap { SourcePath = "full name", TargetField = "Name", Transform = "trim" },
            new ImportFieldMap { SourcePath = "address.city", TargetField = "City", DefaultValue = "Unknown" },
        ],
    };

    private static async Task<Guid> SaveAsync(AutomationTestHost host, ImportSetModel model)
    {
        var saved = await host.WithAsync<IImportSetService, Result<Guid>>(s => s.SaveAsync(model));
        Assert.True(saved.Succeeded, saved.Error);
        return saved.Value;
    }

    private static async Task<PagedResult<ImportRowItem>> RowsAsync(AutomationTestHost host, Guid runId) =>
        (await host.WithAsync<IImportSetService, Result<PagedResult<ImportRowItem>>>(s => s.ListRowsAsync(runId, null, new PageRequest { PageSize = 50 }))).Value!;

    [Fact]
    public async Task CsvImport_InsertsNewRows_AndUpdatesByCoalesceKey()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var existing = host.Records.Seed("contact", new Dictionary<string, object?> { ["Email"] = "grace@example.com", ["Name"] = "Grace H.", ["City"] = "Arlington" });
        var id = await SaveAsync(host, ContactImport());
        const string Csv = "email;full name;address.city\nADA@example.com;  Ada Lovelace ;London\ngrace@example.com;Grace Hopper;New York\n;No Mail;Nowhere\n";

        var run = await host.WithAsync<IImportSetService, Result<Guid>>(s => s.RunCsvAsync(id, "contacts.csv", new MemoryStream(Encoding.UTF8.GetBytes(Csv))));

        Assert.True(run.Succeeded, run.Error);
        var created = Assert.Single(host.Records.Creates);
        Assert.Equal("ada@example.com", created.Values["Email"]);
        Assert.Equal("Ada Lovelace", created.Values["Name"]);
        Assert.Equal("London", created.Values["City"]);
        var update = Assert.Single(host.Records.Updates);
        Assert.Equal(existing, update.Id);
        Assert.Equal("Grace Hopper", update.Values["Name"]);

        var rows = await RowsAsync(host, run.Value);
        Assert.Equal(3, rows.TotalCount);
        Assert.Equal([ImportRowStatus.Inserted, ImportRowStatus.Updated, ImportRowStatus.Error], rows.Items.OrderBy(r => r.RowNumber).Select(r => r.Status));

        var runs = await host.WithAsync<IImportSetService, Result<PagedResult<ImportRunItem>>>(s => s.ListRunsAsync(id, new PageRequest()));
        var summary = Assert.Single(runs.Value!.Items);
        Assert.Equal(ImportRunStatus.CompletedWithErrors, summary.Status);
        Assert.Equal((3, 1, 1, 1), (summary.TotalRows, summary.Inserted, summary.Updated, summary.Errors));
    }

    [Fact]
    public async Task DataSourceImport_MapsJsonArrayAtRecordsPath()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var (sourceId, _) = await IntegrationSeed.AddDataSourceAsync(host);
        host.Invoker.Body = """
            { "data": { "items": [
              { "email": "linus@example.com", "full name": "Linus", "address": { "city": "Portland" } },
              { "email": "margaret@example.com", "full name": "Margaret" }
            ] } }
            """;
        var model = ContactImport(ImportSourceType.DataSource);
        model.DataSourceId = sourceId;
        model.EndpointKey = "list_contacts";
        model.RecordsPath = "data.items";
        var id = await SaveAsync(host, model);

        var run = await host.WithAsync<IImportSetService, Result<Guid>>(s => s.RunNowAsync(id));

        Assert.True(run.Succeeded, run.Error);
        Assert.Equal(2, host.Records.All("contact").Count);
        Assert.Contains(host.Records.All("contact"), r => Equals(r["City"], "Portland"));
        Assert.Contains(host.Records.All("contact"), r => Equals(r["City"], "Unknown"));
    }

    [Fact]
    public async Task DataSourceImport_FailsWhenPathIsNoArray()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var (sourceId, _) = await IntegrationSeed.AddDataSourceAsync(host);
        host.Invoker.Body = """{ "data": { "items": 5 } }""";
        var model = ContactImport(ImportSourceType.DataSource);
        model.DataSourceId = sourceId;
        model.EndpointKey = "list_contacts";
        model.RecordsPath = "data.items";
        var id = await SaveAsync(host, model);

        var run = await host.WithAsync<IImportSetService, Result<Guid>>(s => s.RunNowAsync(id));

        Assert.Empty(host.Records.All("contact"));
        var runs = await host.WithAsync<IImportSetService, Result<PagedResult<ImportRunItem>>>(s => s.ListRunsAsync(id, new PageRequest()));
        Assert.True(!run.Succeeded || runs.Value!.Items.Single().Status == ImportRunStatus.Failed);
    }

    [Fact]
    public async Task Save_ValidatesTransformMap()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var readOnly = ContactImport();
        readOnly.TargetTable = "incident";
        readOnly.CoalesceField = null;
        readOnly.FieldMaps = [new ImportFieldMap { SourcePath = "n", TargetField = "Number" }];
        var badCoalesce = ContactImport();
        badCoalesce.CoalesceField = "City2";
        var noTable = ContactImport();
        noTable.TargetTable = "nope";

        var results = new List<Result<Guid>>();
        foreach (var model in new[] { readOnly, badCoalesce, noTable })
        {
            results.Add(await host.WithAsync<IImportSetService, Result<Guid>>(s => s.SaveAsync(model)));
        }

        Assert.All(results, r => Assert.Equal(ResultErrorKind.Validation, r.ErrorKind));
        Assert.Contains("read-only", results[0].Error);
    }

    [Fact]
    public async Task ImportService_RequiresManagePermission()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        host.User.Grant(Permissions.Integration.ReadDataSources);

        var list = await host.WithAsync<IImportSetService, Result<PagedResult<ImportSetListItem>>>(s => s.ListAsync(new PageRequest()));
        var save = await host.WithAsync<IImportSetService, Result<Guid>>(s => s.SaveAsync(ContactImport()));

        Assert.Equal(ResultErrorKind.Forbidden, list.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, save.ErrorKind);
    }

    [Theory]
    [InlineData("a,b\n1,2\n", ',', 2, 1)]
    [InlineData("a;b\r\n1;2\r\n3;4", ';', 2, 2)]
    [InlineData("a\tb\n\"x\ty\"\tz\n", '\t', 2, 1)]
    [InlineData("name,comment\nAda,\"said \"\"hi\"\", then left\"\n", ',', 2, 1)]
    public void CsvParser_DetectsDelimiterAndQuotes(string text, char delimiter, int columns, int rows)
    {
        var parsed = CsvParser.Parse(text, 100);

        Assert.True(parsed.Succeeded, parsed.Error);
        Assert.Equal(delimiter, parsed.Value!.Delimiter);
        Assert.Equal(columns, parsed.Value.Headers.Count);
        Assert.Equal(rows, parsed.Value.Rows.Count);
    }

    [Fact]
    public void CsvParser_HandlesEscapedQuotesAndLimits()
    {
        var parsed = CsvParser.Parse("name,comment\nAda,\"said \"\"hi\"\", then left\"\nB,c\nD,e\n", 2);

        Assert.True(parsed.Succeeded, parsed.Error);
        Assert.Equal("said \"hi\", then left", parsed.Value!.Rows[0][1]);
        Assert.True(parsed.Value.Truncated);
        Assert.Equal(2, parsed.Value.Rows.Count);
        Assert.False(CsvParser.Parse("a,b\n\"unterminated,1\n", 10).Succeeded);
        Assert.False(CsvParser.Parse(string.Empty, 10).Succeeded);
    }
}
