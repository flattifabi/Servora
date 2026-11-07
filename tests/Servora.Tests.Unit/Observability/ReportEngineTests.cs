using Servora.Application.Analytics;
using Servora.Application.Conditions;
using Servora.Application.Security;
using Servora.Domain.Analytics;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Infrastructure.Placeholders;
using Servora.Infrastructure.Platform;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for the report engine, aggregation and key formatting.</summary>
public sealed class ReportEngineTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task GroupedReport_CountsPerChoiceWithLabels()
    {
        await SeedUsersAsync();
        var engine = CreateEngine(Permissions.Identity.ReadUsers);

        var result = await engine.RunAsync(new ReportSpec { TableName = TableNames.User, GroupBy = "Source", ChartType = ReportChartType.Donut });

        Assert.True(result.Succeeded, result.Error);
        var rows = result.Value!.Rows;
        Assert.Equal(6, result.Value.RecordCount);
        Assert.Equal("Local", rows[0].Label);
        Assert.Equal(4, rows[0].Value);
        Assert.Equal(2, rows[1].Value);
        Assert.Equal(6, result.Value.Total);
    }

    [Fact]
    public async Task ReferenceGrouping_ResolvesDisplayValues()
    {
        var owner = new User { UserName = "olivia", NormalizedUserName = "OLIVIA", DisplayName = "Olivia Owner" };
        await using (var db = _database.Create())
        {
            db.Set<User>().Add(owner);
            db.Set<ConfigurationItem>().AddRange(
                new ConfigurationItem { Name = "srv-01", OwnedById = owner.Id, Criticality = 1 },
                new ConfigurationItem { Name = "srv-02", OwnedById = owner.Id, Criticality = 3 },
                new ConfigurationItem { Name = "srv-03", Criticality = 2 });
            await db.SaveChangesAsync();
        }

        var result = await CreateEngine(Permissions.Cmdb.Read).RunAsync(new ReportSpec
        {
            TableName = TableNames.ConfigurationItem,
            GroupBy = "OwnedById",
            Aggregate = ReportAggregate.Average,
            AggregateField = "Criticality",
        });

        Assert.True(result.Succeeded, result.Error);
        var olivia = result.Value!.Rows.Single(r => r.Label == "Olivia Owner");
        Assert.Equal(2, olivia.Value);
        Assert.Equal(2, olivia.Count);
        Assert.Contains(result.Value.Rows, r => r.Label == ReportKeyFormatter.EmptyLabel);
    }

    [Fact]
    public async Task DateGrouping_ProducesChronologicalBuckets()
    {
        await using (var db = _database.Create())
        {
            db.Set<ConfigurationItem>().AddRange(
                new ConfigurationItem { Name = "a", LastDiscoveredAt = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero) },
                new ConfigurationItem { Name = "b", LastDiscoveredAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero) },
                new ConfigurationItem { Name = "c", LastDiscoveredAt = new DateTimeOffset(2026, 1, 30, 0, 0, 0, TimeSpan.Zero) });
            await db.SaveChangesAsync();
        }

        var result = await CreateEngine(Permissions.Cmdb.Read).RunAsync(new ReportSpec
        {
            TableName = TableNames.ConfigurationItem,
            GroupBy = "LastDiscoveredAt",
            DateInterval = ReportDateInterval.Month,
            ChartType = ReportChartType.Line,
        });

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Value!.IsDateSeries);
        Assert.Equal(["Jan 2026", "Mar 2026"], result.Value.Rows.Select(r => r.Label).ToArray());
        Assert.Equal(2, result.Value.Rows[0].Value);
    }

    [Fact]
    public async Task Report_RequiresTableAccess()
    {
        var result = await CreateEngine(Permissions.Analytics.Read).RunAsync(new ReportSpec { TableName = TableNames.User, GroupBy = "Source" });

        Assert.Equal(Application.Common.ResultErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task Report_RejectsUnknownAndHiddenFields()
    {
        var engine = CreateEngine(Permissions.Identity.ReadUsers);

        var hidden = await engine.RunAsync(new ReportSpec { TableName = TableNames.User, GroupBy = "PasswordHash" });
        var notNumeric = await engine.RunAsync(new ReportSpec { TableName = TableNames.User, GroupBy = "Source", Aggregate = ReportAggregate.Sum, AggregateField = "UserName" });

        Assert.Equal(Application.Common.ResultErrorKind.Validation, hidden.ErrorKind);
        Assert.Equal(Application.Common.ResultErrorKind.Validation, notNumeric.ErrorKind);
    }

    [Fact]
    public async Task UnsupportedFilter_RunsUnfilteredWithWarning()
    {
        await SeedUsersAsync();
        var condition = new ConditionGroup { Rules = [new ConditionRule { Field = "UserName", Operator = ConditionOperator.Equals, Value = "u1" }] };

        var result = await CreateEngine(Permissions.Identity.ReadUsers).RunAsync(new ReportSpec { TableName = TableNames.User, GroupBy = "Source", Condition = condition });

        Assert.True(result.Succeeded);
        Assert.Equal(6, result.Value!.RecordCount);
        Assert.Single(result.Value.Warnings);
    }

    [Fact]
    public void Aggregator_FoldsSmallGroupsIntoOther()
    {
        var rows = Enumerable.Range(0, 10).SelectMany(i => Enumerable.Repeat(new ReportProjection { Key = "k" + i }, 10 - i)).ToList();

        var result = ReportAggregator.Aggregate(rows, k => new ReportGroupKey((string)k!, (string)k!, null), ReportAggregate.Count, 4, chronological: false, out var total);

        Assert.Equal(4, result.Count);
        Assert.Equal(["k0", "k1", "k2", ReportAggregator.OtherKey], result.Select(r => r.Key).ToArray());
        Assert.Equal(55, total);
        Assert.Equal(55 - 10 - 9 - 8, result[^1].Value);
    }

    [Fact]
    public void WeekBuckets_StartOnMonday()
    {
        var (start, label) = ReportKeyFormatter.DateBucket(new DateTime(2026, 5, 7), ReportDateInterval.Week);

        Assert.Equal(new DateTime(2026, 5, 4), start);
        Assert.Equal("Wk 04 May", label);
        Assert.Equal("Q2 2026", ReportKeyFormatter.DateBucket(new DateTime(2026, 5, 7), ReportDateInterval.Quarter).Label);
    }

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    private ReportEngine CreateEngine(params string[] permissions) =>
        new(_database, new ReflectionTableRegistry(), new UnavailableRecordService(), new PassThroughConditionEvaluator(), new TestCurrentUser(permissions), _clock);

    private async Task SeedUsersAsync()
    {
        await using var db = _database.Create();
        for (var i = 0; i < 6; i++)
        {
            db.Set<User>().Add(new User
            {
                UserName = "u" + i,
                NormalizedUserName = "U" + i,
                DisplayName = "User " + i,
                Source = i < 4 ? UserSource.Local : UserSource.Directory,
            });
        }

        await db.SaveChangesAsync();
    }
}
