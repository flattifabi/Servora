using Microsoft.EntityFrameworkCore;
using Servora.Domain.Catalog;
using Servora.Infrastructure.Catalog;
using Servora.Tests.Unit.ServiceManagement;

namespace Servora.Tests.Unit.Catalog;

/// <summary>Tests that the catalog seeder creates the portal showcase once and never overwrites administrator changes.</summary>
public sealed class CatalogSeederTests : IDisposable
{
    private readonly TestDatabase _database = new();

    [Fact]
    public async Task Seed_CreatesTheShowcaseCatalog()
    {
        await SeedAsync();

        await using var db = _database.CreateDbContext();
        var hardware = await db.Set<CatalogCategory>().SingleAsync(c => c.Name == "Hardware");
        Assert.Equal(24, await db.Set<CatalogItem>().CountAsync(i => i.CategoryId == hardware.Id && i.IsActive));
        Assert.True(await db.Set<CatalogCategory>().AnyAsync(c => c.Name == CatalogSeeder.AccessCategoryName));
        Assert.True(await db.Set<CatalogCategory>().AnyAsync(c => c.Name == "IT support"));

        var laptop = await db.Set<CatalogItem>().SingleAsync(i => i.Name == "Business laptop");
        Assert.Equal(39m, laptop.Price);
        Assert.Equal(CatalogBillingType.Monthly, laptop.BillingType);
        Assert.Equal(36, laptop.TermMonths);
        Assert.True(laptop.IsFeatured);
        Assert.Equal(3, await db.Set<CatalogSection>().CountAsync(s => s.CatalogItemId == laptop.Id));
        var dock = await db.Set<CatalogVariable>().SingleAsync(v => v.CatalogItemId == laptop.Id && v.Name == "docking_station");
        Assert.Equal(5m, dock.PriceDelta);
        Assert.NotNull(dock.SectionId);

        var vpn = await db.Set<CatalogItem>().SingleAsync(i => i.Name == "VPN access");
        Assert.Equal(0m, vpn.Price);
        Assert.Equal("Available immediately", vpn.DeliveryText);
        Assert.True(vpn.IsFeatured);
    }

    [Fact]
    public async Task Seed_IsIdempotent_AndKeepsAdministratorChanges()
    {
        await SeedAsync();
        int items, variables, sections, categories;
        await using (var db = _database.CreateDbContext())
        {
            items = await db.Set<CatalogItem>().CountAsync();
            variables = await db.Set<CatalogVariable>().CountAsync();
            sections = await db.Set<CatalogSection>().CountAsync();
            categories = await db.Set<CatalogCategory>().CountAsync();

            var laptop = await db.Set<CatalogItem>().SingleAsync(i => i.Name == "Business laptop");
            laptop.Price = 45m;
            laptop.Subtitle = "Changed by the admin";
            var vpn = await db.Set<CatalogItem>().SingleAsync(i => i.Name == "VPN access");
            vpn.Teaser = null;
            vpn.IsFeatured = false;
            db.Set<CatalogItem>().Remove(await db.Set<CatalogItem>().SingleAsync(i => i.Name == "Webcam"));
            await db.SaveChangesAsync();
        }

        await SeedAsync();

        await using var check = _database.CreateDbContext();
        Assert.Equal(items - 1, await check.Set<CatalogItem>().CountAsync());
        Assert.Equal(variables - 1, await check.Set<CatalogVariable>().CountAsync());
        Assert.Equal(sections, await check.Set<CatalogSection>().CountAsync());
        Assert.Equal(categories, await check.Set<CatalogCategory>().CountAsync());
        var changed = await check.Set<CatalogItem>().SingleAsync(i => i.Name == "Business laptop");
        Assert.Equal(45m, changed.Price);
        Assert.Equal("Changed by the admin", changed.Subtitle);
        Assert.False((await check.Set<CatalogItem>().SingleAsync(i => i.Name == "VPN access")).IsFeatured);
    }

    [Fact]
    public async Task Seed_ReusesTheLegacyAccessCategory()
    {
        await using (var db = _database.CreateDbContext())
        {
            db.Set<CatalogCategory>().Add(new CatalogCategory { Name = "Access & Security", Icon = "key" });
            await db.SaveChangesAsync();
        }

        await SeedAsync();

        await using var check = _database.CreateDbContext();
        Assert.False(await check.Set<CatalogCategory>().AnyAsync(c => c.Name == CatalogSeeder.AccessCategoryName));
        var legacy = await check.Set<CatalogCategory>().SingleAsync(c => c.Name == "Access & Security");
        Assert.True(await check.Set<CatalogItem>().AnyAsync(i => i.Name == "VPN access" && i.CategoryId == legacy.Id));
    }

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    private async Task SeedAsync()
    {
        await using var db = _database.CreateDbContext();
        await new CatalogSeeder().SeedAsync(db, CancellationToken.None);
    }
}
