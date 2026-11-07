using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Platform;
using Servora.Domain.Assets;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Assets;

/// <summary>Seeds about 20 demo assets (hardware, software, licenses) when the asset table is empty.</summary>
/// <param name="numbers">The number sequence service.</param>
public sealed class AssetSeeder(INumberSequenceService numbers) : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 120;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Set<Asset>().AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = await db.Set<User>().Where(u => u.NormalizedUserName == "ADMIN").Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        var cis = await db.Set<ConfigurationItem>().Select(c => new { c.Id, c.Name }).Take(500).ToDictionaryAsync(c => c.Name, c => c.Id, cancellationToken);
        Guid? Ci(string name) => cis.TryGetValue(name, out var id) ? id : null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assets = new List<Asset>();

        Asset Add(string name, AssetCategory category, AssetState state, string manufacturer, string model, decimal cost, int purchasedDaysAgo, int warrantyDaysFromToday,
            string? serial = null, string? tag = null, string? location = null, Guid? assignedTo = null, Guid? ci = null, string? vendor = null, int? entitlements = null, int? installs = null, string? notes = null)
        {
            var asset = new Asset
            {
                DisplayName = name,
                Category = category,
                State = state,
                Manufacturer = manufacturer,
                Model = model,
                Cost = cost,
                Currency = "EUR",
                PurchaseDate = today.AddDays(-purchasedDaysAgo),
                WarrantyEnd = today.AddDays(warrantyDaysFromToday),
                SerialNumber = serial,
                AssetTag = tag,
                Location = location,
                AssignedToId = assignedTo,
                ConfigurationItemId = ci,
                Vendor = vendor,
                Entitlements = entitlements,
                Installs = installs,
                Notes = notes,
                RetiredAt = state is AssetState.Retired or AssetState.Disposed ? DateTimeOffset.UtcNow.AddDays(-20) : null,
            };
            assets.Add(asset);
            return asset;
        }

        // End-user hardware
        Add("MacBook Pro 14\" – System Administrator", AssetCategory.Hardware, AssetState.InUse, "Apple", "MacBook Pro 14 M3 Pro", 2_499m, 240, 490, "C02FX3K1Q6L4", "IT-000101", "HQ Berlin · Floor 3", admin, vendor: "Gravis Business");
        Add("Dell Latitude 7440", AssetCategory.Hardware, AssetState.InStock, "Dell", "Latitude 7440", 1_649m, 120, 975, "7HQK9Z3", "IT-000102", "HQ Berlin · IT Store", vendor: "Dell Direct");
        Add("Dell Latitude 7440", AssetCategory.Hardware, AssetState.InStock, "Dell", "Latitude 7440", 1_649m, 120, 975, "7HQK9Z4", "IT-000103", "HQ Berlin · IT Store", vendor: "Dell Direct");
        Add("Lenovo ThinkPad X1 Carbon Gen 11", AssetCategory.Hardware, AssetState.InRepair, "Lenovo", "ThinkPad X1 Carbon Gen 11", 1_899m, 610, 120, "PF4ABC12", "IT-000087", "Repair centre", vendor: "Bechtle", notes: "Keyboard replacement under warranty (RMA 55-1187).");
        Add("Lenovo ThinkPad T14 Gen 3", AssetCategory.Hardware, AssetState.Retired, "Lenovo", "ThinkPad T14 Gen 3", 1_299m, 1_460, -365, "PF2XYZ98", "IT-000031", "HQ Berlin · IT Store", vendor: "Bechtle", notes: "End of life – awaiting certified data wipe.");
        Add("Dell UltraSharp 27 4K", AssetCategory.Hardware, AssetState.InUse, "Dell", "U2723QE", 589m, 300, 795, "CN0U27Q-4K1", "IT-000110", "HQ Berlin · Floor 3", admin, vendor: "Dell Direct");
        Add("iPhone 15 Pro", AssetCategory.Hardware, AssetState.InUse, "Apple", "iPhone 15 Pro 256 GB", 1_329m, 330, 35, "F2LXK9P0Q1", "MOB-00045", "HQ Berlin", admin, vendor: "Telekom Business", notes: "AppleCare+ ends soon – decide on extension.");
        Add("Samsung Galaxy Tab S9", AssetCategory.Hardware, AssetState.Disposed, "Samsung", "SM-X710", 899m, 1_825, -730, "R52W1234ABC", "MOB-00012", null, notes: "Disposed via certified recycler (certificate WEEE-2025-0412).");

        // Data center hardware linked to CIs
        Add("Dell PowerEdge R760 (esx-host-01)", AssetCategory.Hardware, AssetState.InUse, "Dell", "PowerEdge R760", 18_900m, 400, 1_425, "DL7X9K3", "DC-0001", "DC1 Frankfurt · Rack A04", admin, Ci("esx-host-01"), "Dell Direct");
        Add("Dell PowerEdge R760 (esx-host-02)", AssetCategory.Hardware, AssetState.InUse, "Dell", "PowerEdge R760", 18_900m, 400, 1_425, "DL7X9K4", "DC-0002", "DC1 Frankfurt · Rack A05", admin, Ci("esx-host-02"), "Dell Direct");
        Add("HPE ProLiant DL380 Gen11 (sql-cluster-01)", AssetCategory.Hardware, AssetState.InUse, "HPE", "ProLiant DL380 Gen11", 24_500m, 700, 60, "CZ24410XYZ", "DC-0003", "DC1 Frankfurt · Rack B02", admin, Ci("sql-cluster-01"), "HPE Partner", notes: "Warranty renewal quote requested.");
        Add("Lenovo ThinkSystem SR630 V3 (mail-gw-01)", AssetCategory.Hardware, AssetState.InUse, "Lenovo", "ThinkSystem SR630 V3", 7_800m, 900, -15, "J30AB12C", "DC-0004", "DC1 Frankfurt · DMZ Rack", admin, Ci("mail-gw-01"), "Bechtle", notes: "Warranty expired – hardware fault suspected.");
        Add("NetApp AFF A400 (san-prd-01)", AssetCategory.Hardware, AssetState.InUse, "NetApp", "AFF A400", 145_000m, 820, 640, "721845000123", "DC-0005", "DC1 Frankfurt · Rack S01", admin, Ci("san-prd-01"), "NetApp Partner");
        Add("Cisco Catalyst 9500 (core-sw-01)", AssetCategory.Hardware, AssetState.InUse, "Cisco", "C9500-48Y4C", 32_000m, 1_000, 825, "FCW2741L0AB", "DC-0006", "DC1 Frankfurt · Rack N02", admin, Ci("core-sw-01"), "Cisco Partner");
        Add("Palo Alto PA-3410 HA pair (fw-edge-01)", AssetCategory.Hardware, AssetState.InUse, "Palo Alto Networks", "PA-3410", 41_000m, 500, 230, "PA3410-021337", "DC-0007", "DC1 Frankfurt · Rack N01", admin, Ci("fw-edge-01"), "Palo Alto Partner");
        Add("F5 BIG-IP i5800 (lb-prd-01)", AssetCategory.Hardware, AssetState.InUse, "F5", "BIG-IP i5800", 38_500m, 650, 80, "f5-ztmq-7781", "DC-0008", "DC1 Frankfurt · Rack N01", admin, Ci("lb-prd-01"), "F5 Partner");

        // Software and licenses
        Add("SQL Server 2022 Enterprise (16 cores)", AssetCategory.Software, AssetState.InUse, "Microsoft", "SQL Server 2022 Enterprise", 98_000m, 700, 395, ci: Ci("sql-cluster-01"), vendor: "Microsoft EA");
        Add("Microsoft 365 E3", AssetCategory.License, AssetState.InUse, "Microsoft", "M365 E3 (annual)", 81_000m, 300, 65, vendor: "Microsoft CSP", entitlements: 250, installs: 238, notes: "Renewal due – true-up required.");
        Add("Adobe Creative Cloud for Teams", AssetCategory.License, AssetState.InUse, "Adobe", "Creative Cloud All Apps", 21_600m, 200, 165, vendor: "Adobe VIP", entitlements: 25, installs: 27, notes: "Over-deployed: 2 seats to be purchased or reclaimed.");
        Add("Atlassian Jira Software Cloud", AssetCategory.License, AssetState.InUse, "Atlassian", "Jira Software Premium", 14_400m, 150, 215, vendor: "Atlassian", entitlements: 100, installs: 84);
        Add("VMware vSphere Enterprise Plus", AssetCategory.License, AssetState.InUse, "Broadcom", "vSphere Enterprise Plus (per core)", 36_000m, 380, 350, ci: Ci("esx-host-01"), vendor: "Broadcom Partner", entitlements: 128, installs: 128);

        foreach (var asset in assets)
        {
            asset.Number = await numbers.NextAsync(TableNames.Asset, "AST", cancellationToken);
            db.Set<Asset>().Add(asset);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
