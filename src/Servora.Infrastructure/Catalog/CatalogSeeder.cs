using Microsoft.EntityFrameworkCore;
using Servora.Application.Catalog;
using Servora.Application.Common;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Configuration;
using Servora.Domain.Identity;
using Servora.Infrastructure.Changes;

namespace Servora.Infrastructure.Catalog;

/// <summary>
/// Seeds the service catalog. The base set (categories and the original demo items with variables) is matched by name
/// and only created when missing. The portal showcase (business laptop with sections and price impacts,
/// Microsoft 365, the hardware / software / access / support range and the portal presentation of VPN access) is added
/// once, guarded by the system setting <see cref="ShowcaseMarkerKey"/>, so deleted showcase items are not recreated.
/// Existing data is never overwritten: presentation fields are only filled while they are still empty.
/// </summary>
public sealed class CatalogSeeder : IDataSeeder
{
    /// <summary>Name of the demo approval group used by catalog items that require approval.</summary>
    public const string ApproverGroupName = "IT Approvers";

    /// <summary>The system setting that records that the portal showcase catalog was seeded.</summary>
    public const string ShowcaseMarkerKey = "catalog.showcase.seeded";

    /// <summary>Name of the category that contains access and permission services.</summary>
    public const string AccessCategoryName = "Access & permissions";

    /// <inheritdoc />
    public int Order => 120;

    /// <inheritdoc />
    public bool IsDemoData => true;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var serviceDesk = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Service Desk", GroupType.Assignment, "First line support for all employees.", cancellationToken);
        var desktop = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Desktop Support", GroupType.Assignment, "End user devices and workplace services.", cancellationToken);
        var identity = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Identity & Access Management", GroupType.Assignment, "Accounts, roles and access rights.", cancellationToken);
        var network = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Network Operations", GroupType.Assignment, "Network and connectivity services.", cancellationToken);
        var messaging = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Messaging & Collaboration", GroupType.Assignment, "E-mail, chat and collaboration platforms.", cancellationToken);
        var approvers = await ServiceManagementSeedHelper.EnsureGroupAsync(db, ApproverGroupName, GroupType.Approval, "Approves catalog requests that need management consent.", cancellationToken);

        var hardware = await EnsureCategoryAsync(db, "Hardware", "For your workplace", "laptop", 10, [], cancellationToken);
        var software = await EnsureCategoryAsync(db, "Software", "The right tools for you", "grid", 20, [], cancellationToken);
        var access = await EnsureCategoryAsync(db, AccessCategoryName, "Securely connected", "key", 30, ["Access & Security"], cancellationToken);
        var support = await EnsureCategoryAsync(db, "IT support", "We are here to help", "life-buoy", 40, [], cancellationToken);
        var communication = await EnsureCategoryAsync(db, "Communication", "Mailboxes, distribution lists and collaboration.", "mail", 50, [], cancellationToken);
        var onboarding = await EnsureCategoryAsync(db, "People & Onboarding", "Joiners, movers and leavers.", "user-plus", 60, [], cancellationToken);

        var existing = await db.Set<CatalogItem>().Select(i => i.Name).Take(5000).ToListAsync(cancellationToken);

        Add(db, existing, new CatalogItem
        {
            Name = "Software installation", Icon = "package", CategoryId = software, Price = 0m, DeliveryDays = 2, Order = 30,
            ShortDescription = "Install licensed or open source software on your device.",
            Description = "Only software from the approved software list can be installed. Licenses are purchased by IT when required.",
            FulfillmentGroupId = desktop, Code = "SW-010", DeliveryText = "2 working days",
        },
        [
            Var("software_name", "Software", CatalogVariableType.Text, mandatory: true),
            Var("version", "Version", CatalogVariableType.Text, help: "Leave empty for the latest approved version."),
            Choice("license", "License", true, "Existing license available\nPlease purchase a license\nFree / open source", null),
            Var("device_name", "Device name", CatalogVariableType.Text, mandatory: true, help: "Shown under Settings › System › About."),
            Var("notes", "Additional notes", CatalogVariableType.MultilineText),
        ]);

        Add(db, existing, new CatalogItem
        {
            Name = "Access request", Icon = "shield-check", CategoryId = access, Price = 0m, DeliveryDays = 1, Order = 20,
            ShortDescription = "Request access to a business application.",
            Description = "Access is granted according to the principle of least privilege. Your manager and the IT approvers are asked for consent.",
            FulfillmentGroupId = identity, ApprovalRequired = true, ApprovalGroupId = approvers, ManagerApproval = true, Code = "AC-002",
        },
        [
            Choice("application", "Application", true, "SAP ERP\nSalesforce CRM\nJira\nConfluence\nPower BI\nOther", null),
            Var("other_application", "Other application", CatalogVariableType.Text, help: "Only if you selected \"Other\"."),
            Choice("access_level", "Access level", true, "Read only\nContributor\nAdministrator", "Read only"),
            Var("reason", "Why do you need access?", CatalogVariableType.MultilineText, mandatory: true),
            Var("access_until", "Access until", CatalogVariableType.Date, help: "Leave empty for permanent access."),
        ]);

        Add(db, existing, new CatalogItem
        {
            Name = "VPN access", Icon = "shield-check", CategoryId = access, Price = 0m, DeliveryDays = 0, Order = 10,
            ShortDescription = "Securely access internal applications – from your home office or on the go.",
            Description = "VPN access requires multi-factor authentication. Contractor profiles expire automatically.",
            FulfillmentGroupId = network,
        },
        [
            Choice("vpn_profile", "VPN profile", true, "Standard employee\nContractor (time-limited)\nAdministrator", "Standard employee"),
            Choice("device_type", "Device", true, "Company laptop\nPersonal device (BYOD)", "Company laptop"),
            Var("mfa_confirmed", "I have enrolled multi-factor authentication", CatalogVariableType.Boolean, mandatory: true),
            Var("valid_until", "Valid until", CatalogVariableType.Date),
        ]);

        Add(db, existing, new CatalogItem
        {
            Name = "Mobile phone", Icon = "phone", CategoryId = hardware, Price = 650m, DeliveryDays = 4, Order = 30,
            ShortDescription = "Company smartphone with a business data plan.",
            Description = "Phones are enrolled in mobile device management and delivered ready to use.",
            FulfillmentGroupId = desktop, ApprovalRequired = true, ApprovalGroupId = approvers, Code = "HW-020",
        },
        [
            Choice("model", "Model", true, "iPhone 16\nSamsung Galaxy S25\nGoogle Pixel 9", null),
            Choice("data_plan", "Data plan", true, "10 GB Europe\n50 GB Europe\nUnlimited worldwide", "10 GB Europe"),
            Var("number_porting", "Port an existing phone number", CatalogVariableType.Boolean),
            Var("existing_number", "Existing number", CatalogVariableType.Text),
            Var("delivery_location", "Delivery location", CatalogVariableType.Text, mandatory: true),
        ]);

        Add(db, existing, new CatalogItem
        {
            Name = "Password reset", Icon = "unlock", CategoryId = support, Price = 0m, DeliveryDays = 0, Order = 10,
            ShortDescription = "Reset a password or unlock an account.",
            Description = "The service desk verifies the identity of the account owner before resetting a password.",
            FulfillmentGroupId = serviceDesk, DeliveryText = "Usually within 1 hour", Code = "SUP-001",
        },
        [
            Choice("account", "Account", true, "Windows / Active Directory\nSAP\nVPN token\nOther", "Windows / Active Directory"),
            Var("account_owner", "Account owner", CatalogVariableType.UserReference, mandatory: true),
            Var("contact_phone", "Phone number for the call-back", CatalogVariableType.Text),
            Var("unlock_only", "Unlock only (no new password)", CatalogVariableType.Boolean),
        ]);

        Add(db, existing, new CatalogItem
        {
            Name = "New employee onboarding", Icon = "user-plus", CategoryId = onboarding, Price = 0m, DeliveryDays = 10, Order = 10,
            ShortDescription = "Everything a new colleague needs on day one.",
            Description = "Creates the account, mailbox, workstation and standard access for a new employee. Submit at least two weeks before the start date.",
            FulfillmentGroupId = serviceDesk,
            FulfillmentTasks = "Create directory account and mailbox\nPrepare workstation and peripherals\nGrant standard application access\nSchedule the IT welcome session",
            ApprovalRequired = true, ApprovalGroupId = approvers,
        },
        [
            Var("employee_name", "Employee name", CatalogVariableType.Text, mandatory: true),
            Var("start_date", "Start date", CatalogVariableType.Date, mandatory: true),
            Choice("department", "Department", true, "Finance\nHuman Resources\nIT\nMarketing\nOperations\nSales", null),
            Var("job_title", "Job title", CatalogVariableType.Text),
            Var("manager", "Manager", CatalogVariableType.UserReference, mandatory: true),
            Choice("equipment", "Equipment", true, "Standard laptop\nDeveloper workstation\nNo equipment", "Standard laptop"),
            Var("remote_worker", "Works mainly remote", CatalogVariableType.Boolean),
            Var("additional_notes", "Additional notes", CatalogVariableType.MultilineText),
        ]);

        Add(db, existing, new CatalogItem
        {
            Name = "Shared mailbox", Icon = "mail", CategoryId = communication, Price = 0m, DeliveryDays = 2, Order = 10,
            ShortDescription = "A team mailbox that several people can use.",
            Description = "Shared mailboxes have no own license. The owner manages the members.",
            FulfillmentGroupId = messaging,
        },
        [
            Var("mailbox_name", "Display name", CatalogVariableType.Text, mandatory: true),
            Var("email_address", "E-mail address", CatalogVariableType.Text, mandatory: true, maxLength: 120),
            Var("owner", "Owner", CatalogVariableType.UserReference, mandatory: true),
            Var("members", "Members", CatalogVariableType.MultilineText, help: "One user name or e-mail address per line."),
            Var("send_as", "Members may send as the mailbox", CatalogVariableType.Boolean, defaultValue: "true"),
        ]);

        await db.SaveChangesAsync(cancellationToken);

        if (await db.Set<SystemSetting>().AnyAsync(s => s.Key == ShowcaseMarkerKey, cancellationToken))
        {
            return;
        }

        var groups = new SeedGroups(serviceDesk, desktop, identity, network, messaging, approvers);
        AddBusinessLaptop(db, existing, hardware, groups);
        AddMicrosoft365(db, existing, software, groups);
        await PresentVpnAccessAsync(db, cancellationToken);
        AddRange(db, existing, hardware, desktop, CatalogShowcase.Hardware);
        AddRange(db, existing, software, desktop, CatalogShowcase.Software);
        AddRange(db, existing, access, identity, CatalogShowcase.Access);
        AddRange(db, existing, support, serviceDesk, CatalogShowcase.Support);

        db.Set<SystemSetting>().Add(new SystemSetting
        {
            Key = ShowcaseMarkerKey,
            Value = "true",
            Category = "Portal",
            ValueType = "bool",
            Description = "The portal showcase catalog was seeded (deleted showcase items are not recreated).",
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddBusinessLaptop(IAppDbContext db, List<string> existing, Guid hardware, SeedGroups groups)
    {
        var item = new CatalogItem
        {
            Name = "Business laptop", Icon = "laptop", CategoryId = hardware, Order = 10,
            Subtitle = "Lenovo ThinkPad T14 Gen 5",
            ShortDescription = "Your reliable companion. Powerful, mobile and set up for you from day one.",
            Description = "Powerful in the office, flexible on the go. The 14-inch laptop comes with all standard applications and is ready to use right away. Configure it to suit you.",
            BadgeText = "Our standard for you", Code = "HW-001",
            Price = 39m, BillingType = CatalogBillingType.Monthly, TermMonths = 36, OneTimePrice = 0m,
            DeliveryDays = 5, DeliveryText = "3–5 working days",
            Highlights = "package | 3–5 working days\nshield-check | IT support included\nwand | Ready to go, fully set up",
            DetailsMarkdown = "### Equipment\n\n- 14\" WUXGA IPS display (1920 × 1200), anti-glare\n- Intel Core Ultra 5 processor\n- 16 GB RAM (upgrade to 32 GB available)\n- 512 GB NVMe SSD, encrypted\n- Wi-Fi 7, Bluetooth 5.3, USB-C / Thunderbolt 4, HDMI\n- Backlit keyboard, fingerprint reader, IR camera\n\n### Included software\n\nWindows 11 Enterprise, Microsoft 365 apps, Teams, the company VPN client and endpoint protection.",
            DeliveryMarkdown = "### Delivery\n\nThe laptop is delivered **3–5 working days after approval** to the location you enter – fully set up with your account and standard applications.\n\n### Support\n\n- IT support is included for the whole term of **36 months**\n- Defective devices are replaced within one working day\n- Return your old device to the service desk; your data is wiped securely",
            IsFeatured = true, FeaturedOrder = 10,
            FulfillmentGroupId = groups.Desktop, FulfillmentTasks = "Prepare and image the laptop\nDeliver the laptop and collect the old device",
            ApprovalRequired = true, ApprovalGroupId = groups.Approvers, ManagerApproval = true,
        };
        if (existing.Contains(item.Name))
        {
            return;
        }

        var who = new CatalogSection { CatalogItemId = item.Id, Title = "Who is the laptop for?", Order = 10 };
        var device = new CatalogSection { CatalogItemId = item.Id, Title = "Device & accessories", Description = "14\" WUXGA display · Intel Core Ultra 5 · 36-month term", Order = 20 };
        var delivery = new CatalogSection { CatalogItemId = item.Id, Title = "Delivery & demand", Order = 30 };
        db.Set<CatalogSection>().AddRange(who, device, delivery);
        Add(db, existing, item,
        [
            In(who, Choice(CatalogPriceCalculator.CostCenterVariable, "Cost centre", true, "MKT-2040 · Marketing\nFIN-1010 · Finance\nHR-3050 · Human Resources\nIT-4000 · IT\nOPS-6010 · Operations\nSAL-5020 · Sales", null, help: "Assigned to your area automatically.")),
            In(device, Choice("memory", "Memory", true, "16 GB RAM | 0 | Included in the base price\n32 GB RAM | +8.00 | For demanding workloads", "16 GB RAM")),
            In(device, Choice("storage", "Storage", true, "512 GB SSD\n1 TB SSD | +4.00", "512 GB SSD")),
            In(device, Choice("operating_system", "Operating system", true, "Windows 11 Enterprise\nUbuntu 24.04 LTS", "Windows 11 Enterprise")),
            In(device, Priced(Var("docking_station", "USB-C docking station", CatalogVariableType.Boolean, help: "One cable for monitor, network and power."), 5m)),
            In(delivery, Var("delivery_location", "Delivery location", CatalogVariableType.Text, mandatory: true, maxLength: 200, help: "Building, floor and room, for example Berlin office · 3rd floor, room 3.12.")),
            In(delivery, Var("desired_date", "Desired delivery date", CatalogVariableType.Date)),
            In(delivery, Var("business_justification", "Business justification", CatalogVariableType.MultilineText, mandatory: true, maxLength: 500, help: "Helps your manager approve the request quickly.")),
            In(delivery, Var("return_old_device", "I will return my previous device after receiving the new laptop.", CatalogVariableType.Boolean, mandatory: true)),
        ]);
    }

    private static void AddMicrosoft365(IAppDbContext db, List<string> existing, Guid software, SeedGroups groups) =>
        Add(db, existing, new CatalogItem
        {
            Name = "Microsoft 365", Icon = "grid", CategoryId = software, Order = 10,
            Subtitle = "Microsoft 365 E3", Teaser = "Better together.", TeaserText = "Your digital toolkit",
            ShortDescription = "Word, Excel, Teams and more. Everything for productive collaboration.",
            Description = "Microsoft 365 gives you the Office apps on up to five devices, a 100 GB mailbox, 1 TB OneDrive storage and Teams for chat, meetings and calls.",
            Code = "SW-001", Price = 22m, BillingType = CatalogBillingType.Monthly, TermMonths = 12,
            DeliveryDays = 1, DeliveryText = "1 working day",
            Highlights = "clock | 1 working day\ncloud | 1 TB OneDrive storage\nshield-check | Managed and secured by IT",
            DetailsMarkdown = "### What is included\n\n- Word, Excel, PowerPoint, Outlook and OneNote\n- Teams for chat, meetings and calls\n- 100 GB mailbox and 1 TB OneDrive\n- Installation on up to five devices",
            DeliveryMarkdown = "Your license is assigned **within one working day**. You receive an e-mail as soon as you can sign in.",
            IsFeatured = true, FeaturedOrder = 20, FulfillmentGroupId = groups.Messaging,
        },
        [
            Choice("plan", "License", true, "Microsoft 365 E3 | 0 | Office apps, Teams, mailbox and OneDrive\nMicrosoft 365 E5 | +16.00 | Adds advanced security, Power BI Pro and phone system", "Microsoft 365 E3"),
            Choice(CatalogPriceCalculator.CostCenterVariable, "Cost centre", true, "MKT-2040 · Marketing\nFIN-1010 · Finance\nHR-3050 · Human Resources\nIT-4000 · IT\nOPS-6010 · Operations\nSAL-5020 · Sales", null),
            Var("notes", "Notes for IT", CatalogVariableType.MultilineText, maxLength: 500),
        ]);

    private static async Task PresentVpnAccessAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        // Fill only presentation fields that did not exist before (never overwrite administrator changes).
        var vpn = await db.Set<CatalogItem>().FirstOrDefaultAsync(i => i.Name == "VPN access", cancellationToken);
        if (vpn is null || vpn.Teaser is not null || vpn.Subtitle is not null || vpn.IsFeatured)
        {
            return;
        }

        vpn.Teaser = "Always connected.";
        vpn.TeaserText = "Secure. Simple. From anywhere.";
        vpn.DeliveryText = "Available immediately";
        vpn.Code ??= "AC-001";
        vpn.Highlights ??= "zap | Available immediately\nlock | Multi-factor protected\nglobe | Works from anywhere";
        vpn.IsFeatured = true;
        vpn.FeaturedOrder = 30;
    }

    private static void AddRange(IAppDbContext db, List<string> existing, Guid categoryId, Guid fulfillmentGroupId, IReadOnlyList<CatalogShowcaseItem> items)
    {
        foreach (var definition in items)
        {
            Add(db, existing, new CatalogItem
            {
                Name = definition.Name,
                Icon = definition.Icon,
                CategoryId = categoryId,
                Subtitle = definition.Subtitle,
                ShortDescription = definition.ShortDescription,
                Description = definition.ShortDescription,
                Price = definition.Price,
                BillingType = definition.Monthly ? CatalogBillingType.Monthly : CatalogBillingType.OneTime,
                TermMonths = definition.Monthly ? definition.TermMonths : null,
                DeliveryDays = definition.DeliveryDays,
                DeliveryText = definition.DeliveryText,
                Code = definition.Code,
                Order = definition.Order,
                FulfillmentGroupId = fulfillmentGroupId,
            },
            [Var("notes", "Notes for IT", CatalogVariableType.MultilineText, maxLength: 500)]);
        }
    }

    private static void Add(IAppDbContext db, List<string> existing, CatalogItem item, IReadOnlyList<CatalogVariable> variables)
    {
        if (existing.Contains(item.Name))
        {
            return;
        }

        item.ApprovalMode = ApprovalMode.AnyApprover;
        db.Set<CatalogItem>().Add(item);
        for (var i = 0; i < variables.Count; i++)
        {
            variables[i].CatalogItemId = item.Id;
            variables[i].Order = (i + 1) * 10;
            db.Set<CatalogVariable>().Add(variables[i]);
        }

        existing.Add(item.Name);
    }

    private static CatalogVariable Var(string name, string label, CatalogVariableType type, bool mandatory = false, string? help = null, int? maxLength = null, string? defaultValue = null) => new()
    {
        Name = name,
        Label = label,
        Type = type,
        Mandatory = mandatory,
        HelpText = help,
        MaxLength = maxLength,
        DefaultValue = defaultValue,
    };

    private static CatalogVariable Choice(string name, string label, bool mandatory, string options, string? defaultValue, string? help = null) => new()
    {
        Name = name,
        Label = label,
        Type = CatalogVariableType.Choice,
        Mandatory = mandatory,
        Options = options,
        DefaultValue = defaultValue,
        HelpText = help,
    };

    private static CatalogVariable In(CatalogSection section, CatalogVariable variable)
    {
        variable.SectionId = section.Id;
        return variable;
    }

    private static CatalogVariable Priced(CatalogVariable variable, decimal priceDelta)
    {
        variable.PriceDelta = priceDelta;
        return variable;
    }

    private static async Task<Guid> EnsureCategoryAsync(IAppDbContext db, string name, string description, string icon, int order, IReadOnlyList<string> aliases, CancellationToken cancellationToken)
    {
        var names = aliases.Prepend(name).ToList();
        var matches = await db.Set<CatalogCategory>().Where(c => names.Contains(c.Name)).Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken);
        var match = matches.FirstOrDefault(c => c.Name == name) ?? matches.FirstOrDefault();
        if (match is not null)
        {
            return match.Id;
        }

        var category = new CatalogCategory { Name = name, Description = description, Icon = icon, Order = order };
        db.Set<CatalogCategory>().Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}
