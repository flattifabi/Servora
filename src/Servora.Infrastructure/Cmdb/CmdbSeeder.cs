using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Cmdb;
using Servora.Application.Common;
using Servora.Domain.Cmdb;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Cmdb;

/// <summary>
/// Seeds the built-in CI classes (idempotent: missing classes are added) and – only when the CMDB is empty – a demo service
/// map of about 30 configuration items with meaningful relationships (business services → applications → servers / VMs →
/// hypervisors → storage and network).
/// </summary>
public sealed class CmdbSeeder : IDataSeeder
{
    /// <summary>The discovery source written on seeded CIs.</summary>
    public const string SeedSource = "Demo data";

    /// <inheritdoc />
    public int Order => 110;

    /// <inheritdoc />
    public bool IsDemoData => true;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        await SeedClassesAsync(db, cancellationToken);
        if (await db.Set<ConfigurationItem>().AnyAsync(cancellationToken))
        {
            return;
        }

        await SeedServiceMapAsync(db, cancellationToken);
    }

    private static CiAttributeDefinition Attr(string name, string label, CiAttributeType type, string? unit = null, bool required = false, params string[] choices) =>
        new() { Name = name, Label = label, Type = type, Unit = unit, Required = required, Choices = [.. choices] };

    internal static async Task SeedClassesAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var definitions = new List<CiClass>
        {
            NewClass("business_service", "Business Service", "target", "#a855f7", null, 10, "A service delivered to the business or to customers.",
            [
                Attr("service_tier", "Service tier", CiAttributeType.Choice, null, false, "Platinum", "Gold", "Silver", "Bronze"),
                Attr("availability_target", "Availability target", CiAttributeType.Number, "%"),
                Attr("customer_facing", "Customer facing", CiAttributeType.Boolean),
                Attr("business_unit", "Business unit", CiAttributeType.Text),
            ]),
            NewClass("application", "Application", "grid", "#7c6cff", null, 20, "Software that implements (part of) a business service.",
            [
                Attr("app_version", "Version", CiAttributeType.Text),
                Attr("technology", "Technology", CiAttributeType.Choice, null, false, ".NET", "Java", "Node.js", "Python", "Go", "SaaS", "Other"),
                Attr("repository", "Source repository", CiAttributeType.Text),
                Attr("support_end", "Vendor support end", CiAttributeType.Date),
            ]),
            NewClass("server", "Server", "server", "#22d3ee", null, 30, "Physical or virtual computer providing compute capacity.",
            [
                Attr("os", "Operating system", CiAttributeType.Choice, null, false, "Windows Server 2022", "Windows Server 2019", "Ubuntu 24.04", "Ubuntu 22.04", "RHEL 9", "Debian 12", "VMware ESXi 8"),
                Attr("cpu_cores", "CPU cores", CiAttributeType.Number),
                Attr("ram_gb", "Memory", CiAttributeType.Number, "GB"),
                Attr("disk_gb", "Disk", CiAttributeType.Number, "GB"),
            ]),
            NewClass("virtual_machine", "Virtual Machine", "cpu", "#3b82f6", "server", 35, "A server virtualised on a hypervisor host.",
            [
                Attr("hypervisor", "Hypervisor", CiAttributeType.Choice, null, false, "VMware ESXi", "Hyper-V", "KVM"),
                Attr("cluster", "Cluster", CiAttributeType.Text),
            ]),
            NewClass("database", "Database", "database", "#f59e0b", null, 40, "A database instance or schema.",
            [
                Attr("engine", "Engine", CiAttributeType.Choice, null, false, "SQL Server", "PostgreSQL", "Oracle", "MySQL", "MongoDB"),
                Attr("engine_version", "Engine version", CiAttributeType.Text),
                Attr("size_gb", "Size", CiAttributeType.Number, "GB"),
                Attr("backup_policy", "Backup policy", CiAttributeType.Choice, null, false, "Hourly", "Daily", "Weekly"),
            ]),
            NewClass("network_device", "Network Device", "network", "#22c55e", null, 50, "Router, switch, firewall or load balancer.",
            [
                Attr("device_type", "Device type", CiAttributeType.Choice, null, false, "Router", "Switch", "Firewall", "Load balancer", "Wireless controller"),
                Attr("firmware", "Firmware", CiAttributeType.Text),
                Attr("port_count", "Ports", CiAttributeType.Number),
            ]),
            NewClass("storage", "Storage", "hard-drive", "#f43f5e", null, 60, "SAN, NAS or object storage system.",
            [
                Attr("storage_type", "Storage type", CiAttributeType.Choice, null, false, "SAN", "NAS", "Object"),
                Attr("capacity_tb", "Capacity", CiAttributeType.Number, "TB"),
                Attr("raid_level", "RAID level", CiAttributeType.Text),
            ]),
        };

        var existing = await db.Set<CiClass>().Select(c => c.Key).ToListAsync(cancellationToken);
        foreach (var definition in definitions.Where(d => !existing.Contains(d.Key)))
        {
            db.Set<CiClass>().Add(definition);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static CiClass NewClass(string key, string label, string icon, string color, string? parent, int order, string description, List<CiAttributeDefinition> attributes) => new()
    {
        Key = key,
        Label = label,
        Icon = icon,
        Color = color,
        ParentKey = parent,
        SortOrder = order,
        Description = description,
        IsSystem = true,
        AttributesJson = CiAttributeSchema.SerializeDefinitions(attributes),
    };

    private static async Task SeedServiceMapAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var admin = await db.Set<User>().Where(u => u.NormalizedUserName == "ADMIN").Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        var groups = await db.Set<Group>().Select(g => new { g.Id, g.Name }).Take(200).ToListAsync(cancellationToken);
        Guid? GroupId(string name) => groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;
        var serviceDesk = GroupId("Service Desk");
        var network = GroupId("Network Operations");
        var dba = GroupId("Database Administration");
        var now = DateTimeOffset.UtcNow;
        var cis = new Dictionary<string, ConfigurationItem>(StringComparer.Ordinal);

        ConfigurationItem Add(string name, string cls, string description, int criticality, object attributes, string? ip = null, string? fqdn = null,
            string? manufacturer = null, string? model = null, string? location = null, OperationalStatus status = OperationalStatus.Operational, Guid? group = null, string? serial = null)
        {
            var custom = new JsonObject();
            foreach (var property in attributes.GetType().GetProperties())
            {
                custom[property.Name] = property.GetValue(attributes) switch
                {
                    bool b => JsonValue.Create(b),
                    { } v => JsonValue.Create(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)),
                    _ => null,
                };
            }

            var ci = new ConfigurationItem
            {
                Name = name,
                ClassName = cls,
                Description = description,
                Criticality = criticality,
                Environment = "Production",
                IpAddress = ip,
                Fqdn = fqdn,
                Manufacturer = manufacturer,
                Model = model,
                Location = location,
                OperationalStatus = status,
                OwnedById = admin,
                SupportGroupId = group ?? serviceDesk,
                SerialNumber = serial,
                DiscoverySource = SeedSource,
                LastDiscoveredAt = now.AddHours(-cis.Count),
                CustomFieldsJson = custom.Count == 0 ? null : custom.ToJsonString(),
            };
            cis[name] = ci;
            db.Set<ConfigurationItem>().Add(ci);
            return ci;
        }

        // Business services
        Add("Online Shop", "business_service", "Public e-commerce storefront including checkout and order tracking.", 1, new { service_tier = "Platinum", availability_target = 99.95, customer_facing = true, business_unit = "Digital Sales" });
        Add("Customer Relationship Management", "business_service", "Sales pipeline, accounts and customer service case management.", 2, new { service_tier = "Gold", availability_target = 99.5, customer_facing = false, business_unit = "Sales" });
        Add("Corporate Email", "business_service", "E-mail, calendars and distribution lists for all employees.", 1, new { service_tier = "Gold", availability_target = 99.9, customer_facing = false, business_unit = "IT" });
        Add("HR Self-Service", "business_service", "Employee self-service for leave, payslips and personal data.", 3, new { service_tier = "Silver", availability_target = 99.0, customer_facing = false, business_unit = "Human Resources" });

        // Applications
        Add("Shop Web Frontend", "application", "Server-side rendered storefront (product catalog, cart, checkout UI).", 1, new { app_version = "4.12.0", technology = ".NET", repository = "git.example.com/shop/web" });
        Add("Order Service API", "application", "REST API for orders, inventory reservations and fulfilment events.", 1, new { app_version = "2.8.3", technology = "Java", repository = "git.example.com/shop/orders" });
        Add("Payment Gateway Connector", "application", "Integration with the external payment service provider (PCI scope).", 1, new { app_version = "1.5.1", technology = "Go", repository = "git.example.com/shop/payments" });
        Add("CRM Application", "application", "Customer relationship management suite, on-premises installation.", 2, new { app_version = "2024 R2", technology = ".NET", support_end = "2029-06-30" });
        Add("Mail Transport Service", "application", "Hybrid mail transport, spam filtering and journaling.", 1, new { app_version = "15.2", technology = "Other", support_end = "2027-10-14" });
        Add("HR Portal", "application", "Self-service web portal for employees and managers.", 3, new { app_version = "3.1.0", technology = "Node.js", repository = "git.example.com/hr/portal" });
        Add("Identity Provider (SSO)", "application", "Single sign-on (SAML / OpenID Connect) and MFA for internal applications.", 1, new { app_version = "25.0.4", technology = "Java", support_end = "2028-03-31" });

        // Virtual machines
        Add("web-prd-01", "virtual_machine", "Storefront web node 1.", 2, new { os = "Ubuntu 24.04", cpu_cores = 8, ram_gb = 32, disk_gb = 120, hypervisor = "VMware ESXi", cluster = "PRD-CL01" }, "10.10.20.11", "web-prd-01.corp.example.com", "VMware", "Virtual Platform", "DC1 Frankfurt");
        Add("web-prd-02", "virtual_machine", "Storefront web node 2.", 2, new { os = "Ubuntu 24.04", cpu_cores = 8, ram_gb = 32, disk_gb = 120, hypervisor = "VMware ESXi", cluster = "PRD-CL01" }, "10.10.20.12", "web-prd-02.corp.example.com", "VMware", "Virtual Platform", "DC1 Frankfurt", OperationalStatus.Maintenance);
        Add("app-prd-01", "virtual_machine", "Order API node 1.", 1, new { os = "RHEL 9", cpu_cores = 16, ram_gb = 64, disk_gb = 200, hypervisor = "VMware ESXi", cluster = "PRD-CL01" }, "10.10.30.21", "app-prd-01.corp.example.com", "VMware", "Virtual Platform", "DC1 Frankfurt");
        Add("app-prd-02", "virtual_machine", "Order API node 2 and payment connector.", 1, new { os = "RHEL 9", cpu_cores = 16, ram_gb = 64, disk_gb = 200, hypervisor = "VMware ESXi", cluster = "PRD-CL01" }, "10.10.30.22", "app-prd-02.corp.example.com", "VMware", "Virtual Platform", "DC1 Frankfurt");
        Add("crm-app-01", "virtual_machine", "CRM application server.", 2, new { os = "Windows Server 2022", cpu_cores = 8, ram_gb = 48, disk_gb = 250, hypervisor = "VMware ESXi", cluster = "PRD-CL01" }, "10.10.40.31", "crm-app-01.corp.example.com", "VMware", "Virtual Platform", "DC1 Frankfurt");
        Add("hr-app-01", "virtual_machine", "HR portal application server.", 3, new { os = "Ubuntu 22.04", cpu_cores = 4, ram_gb = 16, disk_gb = 100, hypervisor = "VMware ESXi", cluster = "PRD-CL01" }, "10.10.40.41", "hr-app-01.corp.example.com", "VMware", "Virtual Platform", "DC1 Frankfurt");
        Add("idp-prd-01", "virtual_machine", "Identity provider node.", 1, new { os = "Debian 12", cpu_cores = 8, ram_gb = 16, disk_gb = 80, hypervisor = "VMware ESXi", cluster = "PRD-CL01" }, "10.10.50.51", "sso.corp.example.com", "VMware", "Virtual Platform", "DC1 Frankfurt");

        // Physical servers
        Add("esx-host-01", "server", "VMware ESXi hypervisor host, cluster PRD-CL01.", 1, new { os = "VMware ESXi 8", cpu_cores = 64, ram_gb = 1024, disk_gb = 960 }, "10.10.1.11", "esx-host-01.corp.example.com", "Dell", "PowerEdge R760", "DC1 Frankfurt · Rack A04", serial: "DL7X9K3");
        Add("esx-host-02", "server", "VMware ESXi hypervisor host, cluster PRD-CL01.", 1, new { os = "VMware ESXi 8", cpu_cores = 64, ram_gb = 1024, disk_gb = 960 }, "10.10.1.12", "esx-host-02.corp.example.com", "Dell", "PowerEdge R760", "DC1 Frankfurt · Rack A05", serial: "DL7X9K4");
        Add("sql-cluster-01", "server", "SQL Server failover cluster (2 nodes).", 1, new { os = "Windows Server 2022", cpu_cores = 32, ram_gb = 512, disk_gb = 1920 }, "10.10.60.10", "sql-cluster-01.corp.example.com", "HPE", "ProLiant DL380 Gen11", "DC1 Frankfurt · Rack B02", group: dba, serial: "CZ24410XYZ");
        Add("mail-gw-01", "server", "Mail gateway appliance in the DMZ.", 1, new { os = "Ubuntu 22.04", cpu_cores = 8, ram_gb = 32, disk_gb = 480 }, "172.16.0.25", "mx1.example.com", "Lenovo", "ThinkSystem SR630 V3", "DC1 Frankfurt · DMZ Rack", OperationalStatus.NonOperational, serial: "J30AB12C");

        // Databases
        Add("shop-db-prd", "database", "Orders and catalog database (primary + streaming replica).", 1, new { engine = "PostgreSQL", engine_version = "16.4", size_gb = 850, backup_policy = "Hourly" }, "10.10.60.21", "shop-db.corp.example.com", group: dba);
        Add("crm-db-prd", "database", "CRM database (availability group).", 2, new { engine = "SQL Server", engine_version = "2022 CU14", size_gb = 1200, backup_policy = "Daily" }, null, "crm-db.corp.example.com", group: dba);
        Add("hr-db-prd", "database", "HR portal database.", 3, new { engine = "SQL Server", engine_version = "2022 CU14", size_gb = 140, backup_policy = "Daily" }, null, "hr-db.corp.example.com", group: dba);
        Add("idp-db-prd", "database", "Identity provider realm and session store.", 1, new { engine = "PostgreSQL", engine_version = "16.4", size_gb = 35, backup_policy = "Hourly" }, "10.10.60.22", "idp-db.corp.example.com", group: dba);

        // Network
        Add("lb-prd-01", "network_device", "Application delivery controller for public web traffic.", 1, new { device_type = "Load balancer", firmware = "17.1.1", port_count = 16 }, "10.10.10.5", "lb-prd-01.corp.example.com", "F5", "BIG-IP i5800", "DC1 Frankfurt · Rack N01", group: network, serial: "f5-ztmq-7781");
        Add("fw-edge-01", "network_device", "Internet edge firewall (HA pair).", 1, new { device_type = "Firewall", firmware = "PAN-OS 11.1.4", port_count = 24 }, "10.10.10.1", "fw-edge-01.corp.example.com", "Palo Alto Networks", "PA-3410", "DC1 Frankfurt · Rack N01", group: network, serial: "PA3410-021337");
        Add("core-sw-01", "network_device", "Data center core switch.", 1, new { device_type = "Switch", firmware = "IOS XE 17.12", port_count = 48 }, "10.10.0.1", "core-sw-01.corp.example.com", "Cisco", "Catalyst 9500-48Y4C", "DC1 Frankfurt · Rack N02", group: network, serial: "FCW2741L0AB");

        // Storage
        Add("san-prd-01", "storage", "All-flash SAN for virtualisation and databases.", 1, new { storage_type = "SAN", capacity_tb = 240, raid_level = "RAID-DP" }, "10.10.70.10", "san-prd-01.corp.example.com", "NetApp", "AFF A400", "DC1 Frankfurt · Rack S01", serial: "721845000123");
        Add("nas-backup-01", "storage", "Backup target with immutable snapshots.", 2, new { storage_type = "NAS", capacity_tb = 480, raid_level = "RAID 6" }, "10.10.70.20", "nas-backup-01.corp.example.com", "Synology", "HD6500", "DC2 Munich · Rack S03", serial: "2240RHN012345");

        await db.SaveChangesAsync(cancellationToken);

        void Rel(string parent, string type, string child)
        {
            var relationshipType = type switch
            {
                "runs" => CiRelationshipType.RunsOn,
                "hosted" => CiRelationshipType.HostedOn,
                "connects" => CiRelationshipType.ConnectsTo,
                "contains" => CiRelationshipType.Contains,
                _ => CiRelationshipType.DependsOn,
            };
            db.Set<CiRelationship>().Add(new CiRelationship { ParentId = cis[parent].Id, ChildId = cis[child].Id, Type = relationshipType });
        }

        Rel("Online Shop", "depends", "Shop Web Frontend");
        Rel("Online Shop", "depends", "Order Service API");
        Rel("Online Shop", "depends", "Payment Gateway Connector");
        Rel("Customer Relationship Management", "depends", "CRM Application");
        Rel("Corporate Email", "depends", "Mail Transport Service");
        Rel("HR Self-Service", "depends", "HR Portal");

        Rel("Shop Web Frontend", "depends", "Order Service API");
        Rel("Shop Web Frontend", "depends", "lb-prd-01");
        Rel("Shop Web Frontend", "runs", "web-prd-01");
        Rel("Shop Web Frontend", "runs", "web-prd-02");
        Rel("Order Service API", "runs", "app-prd-01");
        Rel("Order Service API", "runs", "app-prd-02");
        Rel("Order Service API", "depends", "shop-db-prd");
        Rel("Order Service API", "depends", "Payment Gateway Connector");
        Rel("Payment Gateway Connector", "runs", "app-prd-02");
        Rel("Payment Gateway Connector", "connects", "fw-edge-01");
        Rel("CRM Application", "runs", "crm-app-01");
        Rel("CRM Application", "depends", "crm-db-prd");
        Rel("CRM Application", "depends", "Identity Provider (SSO)");
        Rel("Mail Transport Service", "runs", "mail-gw-01");
        Rel("Mail Transport Service", "depends", "Identity Provider (SSO)");
        Rel("HR Portal", "runs", "hr-app-01");
        Rel("HR Portal", "depends", "hr-db-prd");
        Rel("HR Portal", "depends", "Identity Provider (SSO)");
        Rel("Identity Provider (SSO)", "runs", "idp-prd-01");
        Rel("Identity Provider (SSO)", "depends", "idp-db-prd");

        Rel("web-prd-01", "hosted", "esx-host-01");
        Rel("web-prd-02", "hosted", "esx-host-02");
        Rel("app-prd-01", "hosted", "esx-host-01");
        Rel("app-prd-02", "hosted", "esx-host-02");
        Rel("crm-app-01", "hosted", "esx-host-01");
        Rel("hr-app-01", "hosted", "esx-host-02");
        Rel("idp-prd-01", "hosted", "esx-host-01");

        Rel("shop-db-prd", "runs", "esx-host-02");
        Rel("idp-db-prd", "runs", "esx-host-01");
        Rel("crm-db-prd", "runs", "sql-cluster-01");
        Rel("hr-db-prd", "runs", "sql-cluster-01");

        Rel("esx-host-01", "depends", "san-prd-01");
        Rel("esx-host-02", "depends", "san-prd-01");
        Rel("sql-cluster-01", "depends", "san-prd-01");
        Rel("sql-cluster-01", "depends", "nas-backup-01");
        Rel("san-prd-01", "depends", "nas-backup-01");

        Rel("lb-prd-01", "connects", "web-prd-01");
        Rel("lb-prd-01", "connects", "web-prd-02");
        Rel("lb-prd-01", "connects", "core-sw-01");
        Rel("fw-edge-01", "connects", "core-sw-01");
        Rel("esx-host-01", "connects", "core-sw-01");
        Rel("esx-host-02", "connects", "core-sw-01");
        Rel("sql-cluster-01", "connects", "core-sw-01");
        Rel("mail-gw-01", "connects", "fw-edge-01");

        await db.SaveChangesAsync(cancellationToken);
    }
}
