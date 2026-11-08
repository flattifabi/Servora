using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;
using Servora.Domain.Platform;
using Servora.Infrastructure.Backup;
using Servora.Infrastructure.Persistence;

namespace Servora.Tests.Unit.Persistence.Migrations;

/// <summary>Clean start without sample data, the setup option for sample data and the data reset on real migrations.</summary>
public sealed class DataResetTests
{
    [Fact]
    public async Task Startup_CreatesOnlyAdministratorAndReferenceData()
    {
        using var host = new StartupTestHost();

        await host.InitializeAsync();
        await host.InitializeAsync();

        await using var db = host.CreateContext();
        Assert.Equal("admin", Assert.Single(await db.Set<User>().ToListAsync()).UserName);
        Assert.Empty(await db.Set<Incident>().ToListAsync());
        Assert.NotEmpty(await db.Set<IncidentCategory>().ToListAsync());
        Assert.Equal(0, await CountAsync(db, "cmdb_ci"));
        Assert.True(await CountAsync(db, "cmdb_class") > 0);
    }

    [Fact]
    public async Task Reset_RemovesSampleData_KeepsAdministrator_AndDoesNotComeBack()
    {
        using var host = new StartupTestHost();
        await host.GetRequiredService<DatabaseInitializer>().InitializeAsync(includeDemoData: true);
        await using (var seeded = host.CreateContext())
        {
            Assert.True(await seeded.Set<Incident>().AnyAsync());
            Assert.True(await seeded.Set<User>().CountAsync() > 1);
        }

        var engine = new DataResetEngine(host.GetRequiredService<AppDbContextFactory>());
        var preview = await engine.CountAsync(keepUserId: null, CancellationToken.None);
        var deleted = await engine.ResetAsync(keepUserId: null, CancellationToken.None);

        Assert.Equal(preview.TotalRows, deleted);
        await host.InitializeAsync();
        await using var db = host.CreateContext();
        foreach (var table in DataResetCatalog.Cleared.SelectMany(a => a.Tables))
        {
            Assert.Equal(0, await CountAsync(db, table));
        }

        Assert.Equal("admin", Assert.Single(await db.Set<User>().ToListAsync()).UserName);
        Assert.True(await db.Set<UserRole>().AnyAsync());
        Assert.NotEmpty(await db.Set<IncidentCategory>().ToListAsync());
        Assert.All(await db.Set<NumberSequence>().ToListAsync(), n => Assert.Equal(1001, n.NextValue));
    }

    [Fact]
    public void Catalog_ClassifiesEveryTableExactlyOnce()
    {
        using var host = new StartupTestHost();
        using var db = host.CreateContext();
        var model = db.Model.GetEntityTypes().Select(e => e.GetTableName()).OfType<string>().Distinct().Order().ToList();
        var classified = DataResetCatalog.Cleared.SelectMany(a => a.Tables).Concat(DataResetCatalog.Kept).ToList();

        Assert.Equal(classified.Count, classified.Distinct().Count());
        Assert.Equal(model, classified.Order().ToList());
    }

    private static async Task<int> CountAsync(ServoraDbContext db, string table)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
