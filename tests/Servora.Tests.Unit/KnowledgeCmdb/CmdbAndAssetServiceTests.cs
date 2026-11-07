using Servora.Application.Activity;
using Servora.Application.Assets;
using Servora.Application.Cmdb;
using Servora.Application.Cmdb.Graph;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Domain.Assets;
using Servora.Domain.Cmdb;
using Servora.Infrastructure.Assets;
using Servora.Infrastructure.Cmdb;
using Servora.Infrastructure.Placeholders;
using Servora.Infrastructure.Platform;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Service tests for CMDB relationships / graph and the asset lifecycle.</summary>
public sealed class CmdbAndAssetServiceTests : IDisposable
{
    private readonly KnowledgeCmdbTestDatabase _db = new();
    private readonly TestClock _clock = new();
    private readonly RecordingAuditService _audit = new();
    private readonly TestActor _admin = new("Carl Cmdb", Permissions.Cmdb.Read, Permissions.Cmdb.Write, Permissions.Cmdb.Manage, Permissions.Assets.Read, Permissions.Assets.Write, Permissions.Assets.Manage);
    private readonly TestActor _viewer = new("Vera Viewer", Permissions.Cmdb.Read, Permissions.Assets.Read);

    /// <inheritdoc />
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SeededServiceMap_FormsAConnectedGraph()
    {
        await using (var db = _db.CreateDbContext())
        {
            await new CmdbSeeder().SeedAsync(db, CancellationToken.None);
            await new CmdbSeeder().SeedAsync(db, CancellationToken.None); // idempotent
        }

        var service = Cmdb(_viewer);
        var overview = (await service.GetOverviewAsync()).Value!;
        Assert.InRange(overview.Total, 28, 40);
        Assert.True(overview.RelationshipCount > 40);

        var shop = (await service.ListAsync(new CiQuery { Page = new PageRequest { Search = "Online Shop" } })).Value!.Items.Single();
        var graph = (await service.GetGraphAsync(shop.Id, 4, CiGraphDirection.Downstream)).Value!;
        Assert.Contains(graph.Nodes, n => n.Name == "san-prd-01" && n.Level > 0);
        Assert.All(graph.Nodes, n => Assert.True(n.Level >= 0));

        var san = (await service.ListAsync(new CiQuery { Page = new PageRequest { Search = "san-prd-01" } })).Value!.Items.Single();
        var impact = (await service.GetGraphAsync(san.Id, 6, CiGraphDirection.Upstream)).Value!;
        Assert.Contains(impact.Nodes, n => n.Name == "Online Shop" && n.Level < 0);
        Assert.Contains(impact.Nodes, n => n.Name == "Corporate Email");

        // Class filter includes sub-classes (virtual machines are servers).
        var servers = (await service.ListAsync(new CiQuery { ClassKey = "server", Page = new PageRequest { PageSize = 100 } })).Value!;
        Assert.Contains(servers.Items, c => c.ClassKey == "virtual_machine");
    }

    [Fact]
    public async Task Relationships_AreValidatedAndRemovedWithTheCi()
    {
        await using (var db = _db.CreateDbContext())
        {
            await new CmdbSeeder().SeedAsync(db, CancellationToken.None);
        }

        var service = Cmdb(_admin);
        var app = (await service.CreateAsync(new CiInput { Name = "Billing", ClassName = "application", Attributes = { ["technology"] = "Go" } })).Value;
        var vm = (await service.CreateAsync(new CiInput { Name = "billing-01", ClassName = "virtual_machine", IpAddress = "10.0.0.5", Attributes = { ["ram_gb"] = "16" } })).Value;

        Assert.Equal(ResultErrorKind.Validation, (await service.CreateAsync(new CiInput { Name = "bad", ClassName = "server", IpAddress = "999.1.1.1" })).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await service.CreateAsync(new CiInput { Name = "bad", ClassName = "server", Attributes = { ["ram_gb"] = "many" } })).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await service.AddRelationshipAsync(new CiRelationshipInput(app, app, CiRelationshipType.DependsOn))).ErrorKind);
        Assert.True((await service.AddRelationshipAsync(new CiRelationshipInput(app, vm, CiRelationshipType.RunsOn))).Succeeded);
        Assert.Equal(ResultErrorKind.Conflict, (await service.AddRelationshipAsync(new CiRelationshipInput(app, vm, CiRelationshipType.RunsOn))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Cmdb(_viewer).AddRelationshipAsync(new CiRelationshipInput(vm, app, CiRelationshipType.DependsOn))).ErrorKind);

        var relationships = (await service.GetRelationshipsAsync(vm)).Value!;
        Assert.Single(relationships);
        Assert.Equal("Billing", relationships[0].ParentName);

        Assert.Equal(ResultErrorKind.Forbidden, (await Cmdb(_viewer).DeleteAsync(app)).ErrorKind);
        Assert.True((await service.DeleteAsync(app)).Succeeded);
        Assert.Empty((await service.GetRelationshipsAsync(vm)).Value!);
        Assert.Contains(_audit.Entries, e => e.Action == "ci.delete");
    }

    [Fact]
    public async Task Asset_LifecycleIsEnforced()
    {
        var service = Assets(_admin);
        var created = await service.CreateAsync(new AssetInput { DisplayName = "Laptop", Category = AssetCategory.Hardware, Cost = 1200.50m, Currency = "eur", AssetTag = "IT-1" });
        Assert.True(created.Succeeded, created.Error);
        var id = created.Value;

        // Hardware must be assigned before it goes into use.
        Assert.Equal(ResultErrorKind.Validation, (await service.ChangeStateAsync(id, AssetState.InUse)).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await service.ChangeStateAsync(id, AssetState.Disposed)).ErrorKind);
        Assert.True((await service.ChangeStateAsync(id, AssetState.InRepair, "Broken hinge")).Succeeded);
        Assert.True((await service.ChangeStateAsync(id, AssetState.Retired)).Succeeded);
        Assert.True((await service.ChangeStateAsync(id, AssetState.Disposed)).Succeeded);
        Assert.Contains(_audit.Entries, e => e.Action == "asset.dispose");

        var detail = (await service.GetAsync(id)).Value!;
        Assert.Equal(AssetState.Disposed, detail.State);
        Assert.Equal("EUR", detail.Values.Currency);
        Assert.StartsWith("AST", detail.Number, StringComparison.Ordinal);
        Assert.Empty(detail.AllowedTransitions);
        Assert.Equal(ResultErrorKind.Validation, (await service.UpdateAsync(id, detail.Values)).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Assets(_viewer).ChangeStateAsync(id, AssetState.InStock)).ErrorKind);

        // Duplicate asset tags are rejected.
        Assert.Equal(ResultErrorKind.Validation, (await service.CreateAsync(new AssetInput { DisplayName = "Other", AssetTag = "IT-1" })).ErrorKind);
    }

    [Fact]
    public async Task AssetOverview_ComputesValueAndLicenseCompliance()
    {
        var service = Assets(_admin);
        await service.CreateAsync(new AssetInput { DisplayName = "Server", Cost = 1000m, WarrantyEnd = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime).AddDays(30) });
        await service.CreateAsync(new AssetInput { DisplayName = "M365", Category = AssetCategory.License, Cost = 500.25m, State = AssetState.InUse, Entitlements = 10, Installs = 12 });

        var overview = (await service.GetOverviewAsync()).Value!;

        Assert.Equal(2, overview.Total);
        Assert.Equal(1500.25m, overview.ActiveValue["EUR"]);
        Assert.Equal(1, overview.WarrantyExpiringSoon);
        Assert.Equal(1, overview.OverDeployedLicenses);
        Assert.Equal(12, overview.PurchasesPerMonth.Count);
    }

    [Fact]
    public async Task AssetSeeder_IsIdempotentAndListFiltersWork()
    {
        await using (var db = _db.CreateDbContext())
        {
            await new CmdbSeeder().SeedAsync(db, CancellationToken.None);
            await new AssetSeeder(new NumberSequenceService(_db)).SeedAsync(db, CancellationToken.None);
            await new AssetSeeder(new NumberSequenceService(_db)).SeedAsync(db, CancellationToken.None);
        }

        var service = Assets(_viewer);
        var all = (await service.ListAsync(new AssetQuery { Page = new PageRequest { PageSize = 100 } })).Value!;
        Assert.InRange(all.TotalCount, 15, 30);
        Assert.Equal(all.TotalCount, all.Items.Select(a => a.Number).Distinct().Count());
        Assert.Contains(all.Items, a => a.Category == AssetCategory.Hardware);
        Assert.Contains(all.Items, a => a.Category == AssetCategory.Software);

        var licenses = (await service.ListAsync(new AssetQuery { Category = AssetCategory.License, Page = new PageRequest { PageSize = 100 } })).Value!;
        Assert.NotEmpty(licenses.Items);
        Assert.All(licenses.Items, a => Assert.Equal(AssetCategory.License, a.Category));
        Assert.All(licenses.Items, a => Assert.NotNull(a.Entitlements));

        var active = (await service.ListAsync(new AssetQuery { ActiveOnly = true, Page = new PageRequest { PageSize = 100 } })).Value!;
        Assert.All(active.Items, a => Assert.True(AssetLifecycle.IsActive(a.State)));

        var first = all.Items[0];
        var found = (await service.ListAsync(new AssetQuery { Page = new PageRequest { Search = first.Number.ToLowerInvariant() } })).Value!;
        Assert.Contains(found.Items, a => a.Id == first.Id);

        Assert.Equal(ResultErrorKind.Forbidden, (await Assets(new TestActor("Nobody")).ListAsync(new AssetQuery())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Assets(_viewer).DeleteAsync(first.Id)).ErrorKind);
    }

    private CmdbService Cmdb(ICurrentUser actor) => new(_db, actor, new DirectoryLookupService(_db), new ActivityService(_db, actor, _clock), _audit,
        new UnavailableRecordService(), new ReflectionTableRegistry());

    private AssetService Assets(ICurrentUser actor) => new(_db, actor, new NumberSequenceService(_db), new DirectoryLookupService(_db), new ActivityService(_db, actor, _clock), _audit, _clock);
}
