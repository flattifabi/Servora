using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Identity;
using Servora.Infrastructure.Changes;

namespace Servora.Infrastructure.Catalog;

/// <summary>
/// Seeds the service catalog: six categories and eight catalog items with variables (new laptop, software
/// installation, access request, VPN access, mobile phone, password reset, new employee onboarding, shared mailbox).
/// Idempotent: categories and items are matched by name and only created when missing.
/// </summary>
public sealed class CatalogSeeder : IDataSeeder
{
    /// <summary>Name of the demo approval group used by catalog items that require approval.</summary>
    public const string ApproverGroupName = "IT Approvers";

    /// <inheritdoc />
    public int Order => 120;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var serviceDesk = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Service Desk", GroupType.Assignment, "First line support for all employees.", cancellationToken);
        var desktop = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Desktop Support", GroupType.Assignment, "End user devices and workplace services.", cancellationToken);
        var identity = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Identity & Access Management", GroupType.Assignment, "Accounts, roles and access rights.", cancellationToken);
        var network = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Network Operations", GroupType.Assignment, "Network and connectivity services.", cancellationToken);
        var messaging = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Messaging & Collaboration", GroupType.Assignment, "E-mail, chat and collaboration platforms.", cancellationToken);
        var approvers = await ServiceManagementSeedHelper.EnsureGroupAsync(db, ApproverGroupName, GroupType.Approval, "Approves catalog requests that need management consent.", cancellationToken);

        var hardware = await EnsureCategoryAsync(db, "Hardware", "Laptops, phones and peripherals.", "laptop", 10, cancellationToken);
        var software = await EnsureCategoryAsync(db, "Software", "Installations and licenses.", "package", 20, cancellationToken);
        var access = await EnsureCategoryAsync(db, "Access & Security", "Accounts, permissions and passwords.", "key", 30, cancellationToken);
        var connectivity = await EnsureCategoryAsync(db, "Network & Connectivity", "VPN, Wi-Fi and remote access.", "globe", 40, cancellationToken);
        var communication = await EnsureCategoryAsync(db, "Communication", "Mailboxes, distribution lists and collaboration.", "mail", 50, cancellationToken);
        var onboarding = await EnsureCategoryAsync(db, "People & Onboarding", "Joiners, movers and leavers.", "user-plus", 60, cancellationToken);

        var existing = await db.Set<CatalogItem>().Select(i => i.Name).ToListAsync(cancellationToken);

        Add(db, existing, new CatalogItem
        {
            Name = "New laptop", Icon = "laptop", CategoryId = hardware, Price = 1450m, DeliveryDays = 5, Order = 10,
            ShortDescription = "Request a new or replacement business laptop.",
            Description = "Standard laptops are delivered pre-configured with the corporate image, encryption and all standard applications.",
            FulfillmentGroupId = desktop, FulfillmentTasks = "Prepare and image the laptop\nDeliver the laptop and collect the old device",
            ApprovalRequired = true, ApprovalGroupId = approvers, ManagerApproval = true,
        },
        [
            Choice("model", "Model", true, "Standard 14\" business laptop\nPerformance 16\" developer laptop\nUltralight 13\" travel laptop", "Standard 14\" business laptop"),
            Choice("operating_system", "Operating system", true, "Windows 11\nUbuntu 24.04 LTS", "Windows 11"),
            Var("replacement", "Is this a replacement for an existing device?", CatalogVariableType.Boolean),
            Var("old_asset_tag", "Asset tag of the old device", CatalogVariableType.Text, help: "Printed on the label below the device, for example AST0001234."),
            Var("business_justification", "Business justification", CatalogVariableType.MultilineText, mandatory: true),
            Var("needed_by", "Needed by", CatalogVariableType.Date),
        ]);

        Add(db, existing, new CatalogItem
        {
            Name = "Software installation", Icon = "package", CategoryId = software, Price = 0m, DeliveryDays = 2, Order = 10,
            ShortDescription = "Install licensed or open source software on your device.",
            Description = "Only software from the approved software list can be installed. Licenses are purchased by IT when required.",
            FulfillmentGroupId = desktop,
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
            Name = "Access request", Icon = "shield-check", CategoryId = access, Price = 0m, DeliveryDays = 1, Order = 10,
            ShortDescription = "Request access to a business application.",
            Description = "Access is granted according to the principle of least privilege. Your manager and the IT approvers are asked for consent.",
            FulfillmentGroupId = identity, ApprovalRequired = true, ApprovalGroupId = approvers, ManagerApproval = true,
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
            Name = "VPN access", Icon = "globe", CategoryId = connectivity, Price = 0m, DeliveryDays = 1, Order = 10,
            ShortDescription = "Secure remote access to the corporate network.",
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
            Name = "Mobile phone", Icon = "phone", CategoryId = hardware, Price = 650m, DeliveryDays = 4, Order = 20,
            ShortDescription = "Company smartphone with a business data plan.",
            Description = "Phones are enrolled in mobile device management and delivered ready to use.",
            FulfillmentGroupId = desktop, ApprovalRequired = true, ApprovalGroupId = approvers,
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
            Name = "Password reset", Icon = "unlock", CategoryId = access, Price = 0m, DeliveryDays = 0, Order = 20,
            ShortDescription = "Reset a password or unlock an account.",
            Description = "The service desk verifies the identity of the account owner before resetting a password.",
            FulfillmentGroupId = serviceDesk,
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

    private static CatalogVariable Choice(string name, string label, bool mandatory, string options, string? defaultValue) => new()
    {
        Name = name,
        Label = label,
        Type = CatalogVariableType.Choice,
        Mandatory = mandatory,
        Options = options,
        DefaultValue = defaultValue,
    };

    private static async Task<Guid> EnsureCategoryAsync(IAppDbContext db, string name, string description, string icon, int order, CancellationToken cancellationToken)
    {
        var id = await db.Set<CatalogCategory>().Where(c => c.Name == name).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
        if (id is { } existing)
        {
            return existing;
        }

        var category = new CatalogCategory { Name = name, Description = description, Icon = icon, Order = order };
        db.Set<CatalogCategory>().Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}
