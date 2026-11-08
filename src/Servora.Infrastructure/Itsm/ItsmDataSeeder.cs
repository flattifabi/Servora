using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Platform;
using Servora.Domain.Activity;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;

namespace Servora.Infrastructure.Itsm;

/// <summary>
/// Seeds incident categories and realistic demo incidents and problems. Idempotent: categories are only seeded when the
/// category table is empty, demo records only when the incident and problem tables are empty.
/// </summary>
/// <remarks>
/// Demo records reference the assignment groups seeded by Identity &amp; Access (T-02) by name; missing groups are
/// created with the same names. Work is assigned to the bootstrap administrator so that "My Work" is meaningful right
/// after installation; callers are taken from existing active users. Configuration items are linked when the CMDB
/// (T-06) seeded them.
/// </remarks>
/// <param name="numbers">The number sequence service.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class ItsmDataSeeder(INumberSequenceService numbers, IClock clock, ILogger<ItsmDataSeeder> logger) : IDataSeeder
{
    private static readonly (string Name, string[] Subcategories)[] s_categories =
    [
        ("Network", ["VPN", "Wi-Fi", "LAN", "DNS", "Firewall"]),
        ("Hardware", ["Laptop", "Desktop", "Printer", "Mobile device", "Monitor"]),
        ("Software", ["Operating system", "Office suite", "Business application", "Browser"]),
        ("Database", ["Performance", "Availability", "Backup"]),
        ("Access & Identity", ["Password reset", "Account locked", "Permissions", "Multi-factor authentication"]),
        ("E-mail & Collaboration", ["Mailbox", "Calendar", "Video conferencing"]),
        ("Inquiry / Help", ["How-to", "Information request"]),
    ];

    private static readonly string[] s_groupNames = ["Service Desk", "Network Operations", "Database Administration", "Application Support"];

    /// <inheritdoc />
    /// <remarks>Runs late so that users, groups and CIs of other modules already exist.</remarks>
    public int Order => 500;

    /// <inheritdoc />
    public bool IsDemoData => true;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        await SeedCategoriesAsync(db, cancellationToken);
        if (await db.Set<Incident>().AnyAsync(cancellationToken) || await db.Set<Problem>().AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = await db.Set<User>().AsNoTracking().Where(u => u.NormalizedUserName == "ADMIN").Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        if (admin is null)
        {
            return;
        }

        var groups = await EnsureGroupsAsync(db, cancellationToken);
        var callers = await db.Set<User>().AsNoTracking()
            .Where(u => u.IsActive && u.Source != UserSource.Service && u.Id != admin)
            .OrderBy(u => u.DisplayName).Select(u => u.Id).Take(20).ToListAsync(cancellationToken);
        if (callers.Count == 0)
        {
            callers.Add(admin.Value);
        }

        var services = await db.Set<ConfigurationItem>().AsNoTracking().Where(c => c.ClassName == "business_service").OrderBy(c => c.Name).Select(c => c.Id).Take(10).ToListAsync(cancellationToken);
        var otherCis = await db.Set<ConfigurationItem>().AsNoTracking().Where(c => c.ClassName != "business_service").OrderBy(c => c.Name).Select(c => c.Id).Take(30).ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        var serial = 0;
        var incidents = new List<Incident>();
        var activity = new List<ActivityEntry>();

        async Task<Incident> AddIncident(
            string shortDescription, string description, string category, string subcategory, ContactChannel channel,
            Impact impact, Urgency urgency, IncidentState state, string group, bool assignToAdmin, double openedHoursAgo,
            bool major = false, double? resolvedHoursAgo = null, IncidentResolutionCode? code = null, string? notes = null,
            OnHoldReason? holdReason = null, string? workNote = null)
        {
            serial++;
            var opened = now.AddHours(-openedHoursAgo);
            var incident = new Incident
            {
                Number = await numbers.NextAsync(TableNames.Incident, Incident.NumberPrefix, cancellationToken),
                ShortDescription = shortDescription,
                Description = description,
                Category = category,
                Subcategory = subcategory,
                Channel = channel,
                Impact = impact,
                Urgency = urgency,
                State = state,
                AssignmentGroupId = groups.GetValueOrDefault(group),
                AssignedToId = assignToAdmin ? admin : null,
                CallerId = callers[serial % callers.Count],
                OpenedById = channel == ContactChannel.Monitoring ? null : admin,
                OpenedAt = opened,
                CreatedAt = opened,
                IsMajorIncident = major,
                OnHoldReason = holdReason,
                BusinessServiceId = services.Count > 0 && serial % 2 == 0 ? services[serial % services.Count] : null,
                ConfigurationItemId = otherCis.Count > 0 && serial % 3 != 0 ? otherCis[serial % otherCis.Count] : null,
            };

            if (state is IncidentState.Resolved or IncidentState.Closed)
            {
                incident.ResolvedAt = now.AddHours(-(resolvedHoursAgo ?? Math.Max(0.5, openedHoursAgo / 2)));
                incident.ResolvedById = admin;
                incident.ResolutionCode = code ?? IncidentResolutionCode.SolvedPermanently;
                incident.ResolutionNotes = notes ?? "Service restored and confirmed with the caller.";
            }

            if (state == IncidentState.Closed)
            {
                incident.ClosedAt = incident.ResolvedAt!.Value.AddHours(24);
                incident.ClosedById = admin;
            }

            incidents.Add(incident);
            activity.Add(Entry(incident.Id, ActivityType.System, $"Incident {incident.Number} opened via {ItsmLabels.For(channel)}.", "System", null, opened));
            if (workNote is not null)
            {
                activity.Add(Entry(incident.Id, ActivityType.WorkNote, workNote, "System Administrator", admin, opened.AddMinutes(35)));
            }

            if (incident.ResolvedAt is { } resolvedAt)
            {
                activity.Add(Entry(incident.Id, ActivityType.System, $"Resolved – {ItsmLabels.For(incident.ResolutionCode!.Value)}.\n{incident.ResolutionNotes}", "System Administrator", admin, resolvedAt));
            }

            return incident;
        }

        var vpn1 = await AddIncident("VPN disconnects every 10–15 minutes for remote sales team", "Since Monday several members of the field sales team report that the GlobalProtect VPN drops roughly every 10–15 minutes. Reconnecting works but open CRM sessions are lost.", "Network", "VPN", ContactChannel.Phone, Impact.Medium, Urgency.High, IncidentState.InProgress, "Network Operations", true, 30, workNote: "Collected client logs from three users – all on client version 6.2.1. Correlates with Monday's client rollout.");
        var vpn2 = await AddIncident("Cannot stay connected to VPN from home office", "VPN connection drops repeatedly; Teams calls freeze and reconnect.", "Network", "VPN", ContactChannel.Portal, Impact.Low, Urgency.Medium, IncidentState.OnHold, "Network Operations", true, 26, holdReason: OnHoldReason.AwaitingProblem);
        var vpn3 = await AddIncident("VPN tunnel resets during large file uploads", "Uploading files larger than 200 MB to SharePoint over VPN fails with a connection reset.", "Network", "VPN", ContactChannel.Email, Impact.Low, Urgency.Low, IncidentState.New, "Network Operations", false, 44);
        var erp1 = await AddIncident("ERP order entry extremely slow – timeouts at month-end", "Order entry screens in the ERP take 30+ seconds to save. Several transactions timed out and had to be re-entered.", "Database", "Performance", ContactChannel.Phone, Impact.High, Urgency.High, IncidentState.InProgress, "Database Administration", true, 9, major: true, workNote: "Deadlock graph captured on ERPDB01: batch job MONTHEND_POST collides with interactive order inserts.");
        var erp2 = await AddIncident("Finance batch job MONTHEND_POST failed with deadlock victim error", "Nightly posting job aborted with SQL error 1205 (deadlock victim). Finance close is blocked.", "Database", "Performance", ContactChannel.Monitoring, Impact.High, Urgency.Medium, IncidentState.InProgress, "Database Administration", false, 8);
        await AddIncident("Customer portal returns HTTP 503 for all users", "External customer portal is down; load balancer health checks failing on all web nodes.", "Software", "Business application", ContactChannel.Monitoring, Impact.High, Urgency.High, IncidentState.Resolved, "Application Support", true, 52, major: true, resolvedHoursAgo: 49, code: IncidentResolutionCode.SolvedPermanently, notes: "Expired TLS certificate on the load balancer replaced; health checks green, portal available again.");
        var printer = await AddIncident("Print spooler crashes when printing from Excel", "Print spooler service stops whenever users print multi-page Excel sheets on the 3rd floor printers.", "Hardware", "Printer", ContactChannel.Portal, Impact.Medium, Urgency.Medium, IncidentState.Closed, "Service Desk", true, 300, resolvedHoursAgo: 260, code: IncidentResolutionCode.SolvedPermanently, notes: "Rolled back printer driver package to 3.4.1 via Intune; spooler stable for 48h.");
        var mail1 = await AddIncident("Outbound e-mails delayed by up to 2 hours", "Users report that external recipients receive e-mails with long delays. Internal mail is not affected.", "E-mail & Collaboration", "Mailbox", ContactChannel.Phone, Impact.Medium, Urgency.Medium, IncidentState.InProgress, "Application Support", false, 30);
        await AddIncident("Password reset for locked account after vacation", "User returned from vacation and the account is locked after several attempts with the old password.", "Access & Identity", "Account locked", ContactChannel.Phone, Impact.Low, Urgency.High, IncidentState.Closed, "Service Desk", true, 200, resolvedHoursAgo: 199.5, code: IncidentResolutionCode.SolvedRemotely, notes: "Account unlocked and temporary password issued after identity verification.");
        await AddIncident("New hire cannot access shared finance drive", "Permissions on \\\\fs01\\finance missing for new controller who started this week.", "Access & Identity", "Permissions", ContactChannel.Portal, Impact.Low, Urgency.Medium, IncidentState.Resolved, "Service Desk", true, 70, resolvedHoursAgo: 46, code: IncidentResolutionCode.SolvedPermanently, notes: "Added user to the FIN-Controllers security group; access confirmed.");
        await AddIncident("Laptop battery swollen – device unsafe to use", "Laptop chassis is bulging near the touchpad. User stopped using the device.", "Hardware", "Laptop", ContactChannel.Chat, Impact.Low, Urgency.High, IncidentState.InProgress, "Service Desk", true, 4, workNote: "Loaner laptop handed over. Replacement ordered under warranty.");
        await AddIncident("Wi-Fi coverage drops in meeting rooms on 5th floor", "Wireless signal drops below usable levels in rooms 5.12–5.18; video calls disconnect.", "Network", "Wi-Fi", ContactChannel.Portal, Impact.Medium, Urgency.Low, IncidentState.New, "Network Operations", false, 52);
        await AddIncident("Teams meetings: audio cuts out for external participants", "External participants report choppy audio in scheduled Teams meetings since yesterday.", "E-mail & Collaboration", "Video conferencing", ContactChannel.Email, Impact.Medium, Urgency.Medium, IncidentState.New, "Application Support", false, 76);
        await AddIncident("Monitor flickering on docking station", "External monitor flickers when connected via USB-C dock.", "Hardware", "Monitor", ContactChannel.Portal, Impact.Low, Urgency.Low, IncidentState.OnHold, "Service Desk", true, 75, holdReason: OnHoldReason.AwaitingVendor, workNote: "Vendor RMA opened for docking station firmware issue.");
        await AddIncident("Backup job for CRM database failed twice", "Nightly full backup of CRMDB failed with I/O error on the backup target.", "Database", "Backup", ContactChannel.Monitoring, Impact.Medium, Urgency.High, IncidentState.Resolved, "Database Administration", false, 34, resolvedHoursAgo: 22, code: IncidentResolutionCode.SolvedWorkaround, notes: "Redirected backup to secondary storage target; primary target under investigation.");
        await AddIncident("DNS resolution failing for internal intranet site", "intranet.corp.local does not resolve from the Berlin office.", "Network", "DNS", ContactChannel.Phone, Impact.Medium, Urgency.High, IncidentState.Closed, "Network Operations", true, 150, resolvedHoursAgo: 148, code: IncidentResolutionCode.SolvedPermanently, notes: "Stale conditional forwarder removed on BER-DC02.");
        await AddIncident("Request: how to set up out-of-office replies", "User asks how to configure automatic replies in Outlook on the web.", "Inquiry / Help", "How-to", ContactChannel.Chat, Impact.Low, Urgency.Low, IncidentState.Resolved, "Service Desk", true, 28, resolvedHoursAgo: 27.5, code: IncidentResolutionCode.SolvedRemotely, notes: "Sent knowledge article and walked the user through the settings.");
        await AddIncident("MFA push notifications not arriving on new phone", "After switching phones the authenticator app does not receive push requests.", "Access & Identity", "Multi-factor authentication", ContactChannel.Phone, Impact.Low, Urgency.High, IncidentState.InProgress, "Service Desk", true, 1.5);
        await AddIncident("Firewall rule blocks partner SFTP transfer", "Partner logistics provider cannot upload delivery files via SFTP since the firewall change last night.", "Network", "Firewall", ContactChannel.Email, Impact.Medium, Urgency.High, IncidentState.New, "Network Operations", false, 100);
        await AddIncident("Browser crashes when opening HR self-service portal", "Edge closes unexpectedly when navigating to the HR portal benefits page.", "Software", "Browser", ContactChannel.Portal, Impact.Low, Urgency.Low, IncidentState.Cancelled, "Application Support", false, 100);
        await AddIncident("Office suite activation error on reimaged desktops", "Office shows 'unlicensed product' after reimaging desktops in the training room.", "Software", "Office suite", ContactChannel.Portal, Impact.Low, Urgency.Medium, IncidentState.Resolved, "Service Desk", false, 120, resolvedHoursAgo: 96, code: IncidentResolutionCode.SolvedPermanently, notes: "Shared computer activation enabled in the deployment profile.");
        await AddIncident("Warehouse scanners cannot sync inventory counts", "Handheld scanners in warehouse B show 'sync failed' when uploading counts.", "Hardware", "Mobile device", ContactChannel.Phone, Impact.Medium, Urgency.Medium, IncidentState.Closed, "Application Support", true, 240, resolvedHoursAgo: 230, code: IncidentResolutionCode.SolvedPermanently, notes: "Renewed expired device certificates via MDM.");
        await AddIncident("Shared calendar not updating for executive assistants", "Delegated calendar changes appear only after several hours.", "E-mail & Collaboration", "Calendar", ContactChannel.Email, Impact.Low, Urgency.Medium, IncidentState.Resolved, "Application Support", true, 60, resolvedHoursAgo: 12, code: IncidentResolutionCode.SolvedPermanently, notes: "Re-created the delegate permissions; synchronisation verified.");
        await AddIncident("Windows update loop on finance laptops", "Several finance laptops reboot repeatedly while installing the latest cumulative update.", "Software", "Operating system", ContactChannel.Monitoring, Impact.Medium, Urgency.Medium, IncidentState.Resolved, "Service Desk", false, 180, resolvedHoursAgo: 140, code: IncidentResolutionCode.SolvedWorkaround, notes: "Paused the update ring and removed the faulty update from affected devices.");
        await AddIncident("ERP report export to Excel produces empty file", "Exporting the open orders report returns a 0 KB file.", "Software", "Business application", ContactChannel.Portal, Impact.Low, Urgency.Low, IncidentState.Closed, "Application Support", false, 320, resolvedHoursAgo: 290, code: IncidentResolutionCode.NotReproducible, notes: "Could not be reproduced after the ERP patch; user confirmed export works.");

        erp2.ParentIncidentId = erp1.Id;

        var prbVpn = await NewProblemAsync("Recurring VPN disconnects after VPN client update 6.2.1", "Multiple incidents report periodic VPN tunnel resets since the rollout of client 6.2.1.", "Network", Impact.Medium, Urgency.High, ProblemState.RootCauseAnalysis, groups.GetValueOrDefault("Network Operations"), admin, now.AddHours(-24), cancellationToken);
        prbVpn.IsKnownError = true;
        prbVpn.Workaround = "Disable 'Enforce IPv6 tunnel' in the VPN client settings or downgrade to client 6.1.4 via Software Center.";
        var prbErp = await NewProblemAsync("ERP database deadlocks during month-end posting", "Batch job MONTHEND_POST and interactive order entry deadlock on the ORDER_LINES table.", "Database", Impact.High, Urgency.High, ProblemState.FixInProgress, groups.GetValueOrDefault("Database Administration"), admin, now.AddHours(-7), cancellationToken);
        prbErp.RootCause = "Missing index on ORDER_LINES(ORDER_ID, STATUS) causes lock escalation to table level during the posting job.";
        var prbPrinter = await NewProblemAsync("Printer driver package 3.5 crashes the print spooler", "Driver package 3.5.0 distributed via Intune crashes the spooler service on multi-page jobs.", "Hardware", Impact.Medium, Urgency.Medium, ProblemState.Resolved, groups.GetValueOrDefault("Service Desk"), admin, now.AddHours(-280), cancellationToken);
        prbPrinter.RootCause = "Regression in the vendor's rendering filter of driver package 3.5.0.";
        prbPrinter.ResolutionCode = ProblemResolutionCode.FixApplied;
        prbPrinter.FixNotes = "Vendor hotfix 3.5.2 tested and rolled out to all print servers.";
        prbPrinter.ResolvedAt = now.AddHours(-200);
        prbPrinter.ResolvedById = admin;
        var prbMail = await NewProblemAsync("Mail gateway queue saturation delays outbound e-mail", "Outbound queue on the mail gateway regularly exceeds 5,000 messages during business hours.", "E-mail & Collaboration", Impact.Medium, Urgency.Medium, ProblemState.Assess, groups.GetValueOrDefault("Application Support"), null, now.AddHours(-5), cancellationToken);

        vpn1.ProblemId = prbVpn.Id;
        vpn2.ProblemId = prbVpn.Id;
        vpn3.ProblemId = prbVpn.Id;
        erp1.ProblemId = prbErp.Id;
        erp2.ProblemId = prbErp.Id;
        printer.ProblemId = prbPrinter.Id;
        mail1.ProblemId = prbMail.Id;

        var problems = new[] { prbVpn, prbErp, prbPrinter, prbMail };
        foreach (var problem in problems)
        {
            activity.Add(Entry(problem.Id, ActivityType.System, $"Problem {problem.Number} opened.", "System Administrator", admin, problem.OpenedAt, TableNames.Problem));
        }

        activity.Add(Entry(prbVpn.Id, ActivityType.WorkNote, "Vendor case 00482113 opened. Packet captures show the client renegotiating the IPv6 tunnel every 600 s.", "System Administrator", admin, now.AddHours(-20), TableNames.Problem));

        db.Set<Incident>().AddRange(incidents);
        db.Set<Problem>().AddRange(problems);
        db.Set<ActivityEntry>().AddRange(activity);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Incidents} demo incidents and {Problems} demo problems.", incidents.Count, problems.Length);
    }

    private static ActivityEntry Entry(Guid recordId, ActivityType type, string text, string author, Guid? authorId, DateTimeOffset at, string table = TableNames.Incident) => new()
    {
        TableName = table,
        RecordId = recordId,
        Type = type,
        Text = text,
        AuthorName = author,
        AuthorId = authorId,
        CreatedAt = at,
    };

    internal static async Task SeedCategoriesAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Set<IncidentCategory>().AnyAsync(cancellationToken))
        {
            return;
        }

        var order = 0;
        foreach (var (name, subcategories) in s_categories)
        {
            var category = new IncidentCategory { Name = name, Order = order += 10 };
            db.Set<IncidentCategory>().Add(category);
            var subOrder = 0;
            foreach (var sub in subcategories)
            {
                db.Set<IncidentCategory>().Add(new IncidentCategory { Name = sub, ParentId = category.Id, Order = subOrder += 10 });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, Guid>> EnsureGroupsAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Set<Group>().Where(g => s_groupNames.Contains(g.Name)).ToListAsync(cancellationToken);
        foreach (var name in s_groupNames.Where(n => existing.All(g => g.Name != n)))
        {
            var group = new Group { Name = name, Description = $"{name} resolver group.", Type = GroupType.Assignment };
            db.Set<Group>().Add(group);
            existing.Add(group);
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing.ToDictionary(g => g.Name, g => g.Id);
    }

    private async Task<Problem> NewProblemAsync(
        string shortDescription, string description, string category, Impact impact, Urgency urgency, ProblemState state,
        Guid? groupId, Guid? assignee, DateTimeOffset opened, CancellationToken cancellationToken) => new()
    {
        Number = await numbers.NextAsync(TableNames.Problem, Problem.NumberPrefix, cancellationToken),
        ShortDescription = shortDescription,
        Description = description,
        Category = category,
        Impact = impact,
        Urgency = urgency,
        State = state,
        AssignmentGroupId = groupId,
        AssignedToId = assignee,
        OpenedAt = opened,
        CreatedAt = opened,
    };
}
