using Microsoft.EntityFrameworkCore;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Domain.Cmdb;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.Platform;

/// <summary>Tests that conditions translate into SQL (EF Core on SQLite) with the same semantics as in-memory evaluation.</summary>
public sealed class ConditionExpressionTests : IAsyncLifetime
{
    private static readonly Guid s_ownerId = Guid.Parse("2b8a5d1e-7c41-4f3a-8a0e-111111111111");
    private readonly ConditionEvaluator _evaluator = new();
    private PlatformTestHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await PlatformTestHost.CreateAsync();
        await _host.DbAsync(async db =>
        {
            db.Set<User>().Add(new User { Id = s_ownerId, UserName = "owner", NormalizedUserName = "OWNER", DisplayName = "Olivia Owner", Email = "olivia@example.com", PasswordHash = "secret-hash" });
            db.Set<ConfigurationItem>().AddRange(
                new ConfigurationItem { Name = "MAIL-01", ClassName = "server", OperationalStatus = OperationalStatus.Operational, Criticality = 1, OwnedById = s_ownerId, LastDiscoveredAt = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero), Environment = "Production" },
                new ConfigurationItem { Name = "mail-02", ClassName = "server", OperationalStatus = OperationalStatus.Maintenance, Criticality = 2, LastDiscoveredAt = new DateTimeOffset(2026, 3, 16, 9, 0, 0, TimeSpan.Zero), Environment = "Test" },
                new ConfigurationItem { Name = "CRM", ClassName = "application", OperationalStatus = OperationalStatus.Retired, Criticality = 3, Environment = "  " },
                new ConfigurationItem { Name = "Payroll", ClassName = "application", OperationalStatus = OperationalStatus.Operational, Criticality = 4, Environment = null });
            return await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static ConditionGroup Rule(string field, ConditionOperator op, string? value = null) =>
        new() { Rules = [new ConditionRule { Field = field, Operator = op, Value = value }] };

    private Task<List<string>> NamesAsync(ConditionGroup condition, ConditionContext? context = null) =>
        _host.DbAsync(db => db.Set<ConfigurationItem>().AsNoTracking()
            .Where(_evaluator.ToExpression<ConfigurationItem>(condition, context))
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync());

    [Theory]
    [InlineData("Name", ConditionOperator.Equals, "mail-01", "MAIL-01")]
    [InlineData("Name", ConditionOperator.Contains, "MAIL", "MAIL-01,mail-02")]
    [InlineData("Name", ConditionOperator.StartsWith, "pay", "Payroll")]
    [InlineData("Name", ConditionOperator.EndsWith, "02", "mail-02")]
    [InlineData("Name", ConditionOperator.NotContains, "mail", "CRM,Payroll")]
    [InlineData("Name", ConditionOperator.In, "crm, payroll", "CRM,Payroll")]
    [InlineData("Name", ConditionOperator.NotIn, "crm,payroll", "MAIL-01,mail-02")]
    [InlineData("Name", ConditionOperator.NotEquals, "CRM", "MAIL-01,Payroll,mail-02")]
    [InlineData("OperationalStatus", ConditionOperator.Equals, "Operational", "MAIL-01,Payroll")]
    [InlineData("OperationalStatus", ConditionOperator.Equals, "4", "mail-02")]
    [InlineData("OperationalStatus", ConditionOperator.GreaterThan, "Maintenance", "CRM")]
    [InlineData("OperationalStatus", ConditionOperator.In, "Retired,4", "CRM,mail-02")]
    [InlineData("OperationalStatus", ConditionOperator.NotIn, "1", "CRM,mail-02")]
    [InlineData("Criticality", ConditionOperator.GreaterOrEqual, "3", "CRM,Payroll")]
    [InlineData("Criticality", ConditionOperator.LessThan, "2", "MAIL-01")]
    [InlineData("Criticality", ConditionOperator.In, "2,4", "Payroll,mail-02")]
    [InlineData("LastDiscoveredAt", ConditionOperator.Equals, "2026-03-15", "MAIL-01")]
    [InlineData("LastDiscoveredAt", ConditionOperator.GreaterThan, "2026-03-15T12:00:00Z", "mail-02")]
    [InlineData("LastDiscoveredAt", ConditionOperator.LessOrEqual, "2026-03-16T09:00:00Z", "MAIL-01,mail-02")]
    [InlineData("LastDiscoveredAt", ConditionOperator.IsEmpty, null, "CRM,Payroll")]
    [InlineData("Environment", ConditionOperator.IsEmpty, null, "CRM,Payroll")]
    [InlineData("Environment", ConditionOperator.IsNotEmpty, null, "MAIL-01,mail-02")]
    [InlineData("OwnedById", ConditionOperator.IsNotEmpty, null, "MAIL-01")]
    [InlineData("OwnedById", ConditionOperator.Equals, "2b8a5d1e-7c41-4f3a-8a0e-111111111111", "MAIL-01")]
    [InlineData("OwnedById", ConditionOperator.NotEquals, "2b8a5d1e-7c41-4f3a-8a0e-111111111111", "CRM,Payroll,mail-02")]
    [InlineData("name", ConditionOperator.Equals, "crm", "CRM")]
    [InlineData("DoesNotExist", ConditionOperator.Equals, "x", "")]
    [InlineData("Criticality", ConditionOperator.Equals, "not-a-number", "")]
    [InlineData("Criticality", ConditionOperator.NotEquals, "not-a-number", "CRM,MAIL-01,Payroll,mail-02")]
    public async Task Operators_TranslateToSql(string field, ConditionOperator op, string? value, string expected)
    {
        var names = await NamesAsync(Rule(field, op, value));
        Assert.Equal(expected, string.Join(',', names.OrderBy(n => n, StringComparer.Ordinal)));
    }

    [Fact]
    public async Task SqlResults_MatchInMemoryEvaluation()
    {
        var conditions = new[]
        {
            Rule("Name", ConditionOperator.Contains, "a"),
            Rule("OperationalStatus", ConditionOperator.LessOrEqual, "Maintenance"),
            Rule("Environment", ConditionOperator.NotEquals, "Production"),
            Rule("LastDiscoveredAt", ConditionOperator.NotEquals, "2026-03-15"),
            Rule("Criticality", ConditionOperator.NotIn, "1,3"),
        };
        var all = await _host.DbAsync(db => db.Set<ConfigurationItem>().AsNoTracking().ToListAsync());
        foreach (var condition in conditions)
        {
            var sql = await NamesAsync(condition);
            var memory = all.Where(ci => _evaluator.Evaluate(condition, new Dictionary<string, object?>
                {
                    ["Name"] = ci.Name,
                    ["OperationalStatus"] = ci.OperationalStatus,
                    ["Environment"] = ci.Environment,
                    ["LastDiscoveredAt"] = ci.LastDiscoveredAt,
                    ["Criticality"] = ci.Criticality,
                }))
                .Select(ci => ci.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            Assert.Equal(memory, sql.OrderBy(n => n, StringComparer.Ordinal).ToList());
        }
    }

    [Fact]
    public async Task NestedGroups_TranslateToSql()
    {
        var condition = new ConditionGroup
        {
            Logic = ConditionLogic.And,
            Rules = [new ConditionRule { Field = "ClassName", Operator = ConditionOperator.Equals, Value = "server" }],
            Groups =
            [
                new ConditionGroup
                {
                    Logic = ConditionLogic.Or,
                    Rules =
                    [
                        new ConditionRule { Field = "Criticality", Operator = ConditionOperator.Equals, Value = "1" },
                        new ConditionRule { Field = "OperationalStatus", Operator = ConditionOperator.Equals, Value = "Maintenance" },
                    ],
                },
            ],
        };
        Assert.Equal(["MAIL-01", "mail-02"], await NamesAsync(condition));
    }

    [Fact]
    public async Task IsMe_UsesContextUser()
    {
        Assert.Equal(["MAIL-01"], await NamesAsync(Rule("OwnedById", ConditionOperator.IsMe), new ConditionContext(s_ownerId)));
        Assert.Empty(await NamesAsync(Rule("OwnedById", ConditionOperator.IsMe)));
    }

    [Fact]
    public async Task ChangeOperators_AreIgnoredInQueries()
    {
        var condition = new ConditionGroup
        {
            Rules =
            [
                new ConditionRule { Field = "Name", Operator = ConditionOperator.Changed },
                new ConditionRule { Field = "Name", Operator = ConditionOperator.Equals, Value = "CRM" },
            ],
        };
        Assert.Equal(["CRM"], await NamesAsync(condition, new ConditionContext(null, [new RecordFieldChange("Name", "a", "b")])));
    }

    [Fact]
    public async Task NotExposedProperties_NeverMatch()
    {
        var count = await _host.DbAsync(db => db.Set<User>().AsNoTracking()
            .Where(_evaluator.ToExpression<User>(Rule("PasswordHash", ConditionOperator.StartsWith, "secret")))
            .CountAsync());
        Assert.Equal(0, count);

        var notEmpty = await _host.DbAsync(db => db.Set<User>().AsNoTracking()
            .Where(_evaluator.ToExpression<User>(Rule("PasswordHash", ConditionOperator.IsNotEmpty)))
            .CountAsync());
        Assert.Equal(0, notEmpty);
    }

    [Fact]
    public async Task ConditionValues_AreParameterized()
    {
        var malicious = "x'); DROP TABLE cmdb_ci; --";
        Assert.Empty(await NamesAsync(Rule("Name", ConditionOperator.Equals, malicious)));
        var sql = await _host.DbAsync(db => Task.FromResult(db.Set<ConfigurationItem>()
            .Where(_evaluator.ToExpression<ConfigurationItem>(Rule("Name", ConditionOperator.Equals, malicious))).ToQueryString()));
        Assert.DoesNotContain("DROP TABLE", sql.Split('\n').Last(), StringComparison.Ordinal);
        Assert.Equal(4, await _host.DbAsync(db => db.Set<ConfigurationItem>().CountAsync()));
    }
}
