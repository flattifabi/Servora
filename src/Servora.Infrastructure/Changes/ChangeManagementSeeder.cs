using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Changes;
using Servora.Application.Common;
using Servora.Application.Platform;
using Servora.Domain.Approvals;
using Servora.Domain.Changes;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;

namespace Servora.Infrastructure.Changes;

/// <summary>
/// Seeds the "Change Advisory Board" approval group (when missing) and, on an empty change table, a handful of demo
/// changes covering every lifecycle stage (including one waiting for CAB approval). Idempotent.
/// </summary>
/// <param name="numbers">The number sequence service.</param>
/// <param name="clock">The clock.</param>
public sealed class ChangeManagementSeeder(INumberSequenceService numbers, IClock clock) : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 110;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var cabId = await ServiceManagementSeedHelper.EnsureGroupAsync(db, ChangeTables.ChangeAdvisoryBoardGroup, GroupType.Approval,
            "Authorizes normal and emergency changes.", cancellationToken);
        var networkId = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Network Operations", GroupType.Assignment, "Network and connectivity services.", cancellationToken);
        var dbaId = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Database Administration", GroupType.Assignment, "Database platforms and data services.", cancellationToken);
        var desktopId = await ServiceManagementSeedHelper.EnsureGroupAsync(db, "Desktop Support", GroupType.Assignment, "End user devices and workplace services.", cancellationToken);

        if (await db.Set<ChangeRequest>().AnyAsync(cancellationToken))
        {
            return;
        }

        var adminId = await db.Set<User>().Where(u => u.NormalizedUserName == ServiceManagementSeedHelper.AdminNormalizedUserName).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        var ciIds = await db.Set<ConfigurationItem>().OrderBy(c => c.Name).Select(c => c.Id).Take(3).ToListAsync(cancellationToken);
        Guid? Ci(int index) => ciIds.Count > index ? ciIds[index] : null;

        var now = clock.UtcNow;
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var lowRisk = new Dictionary<string, int> { ["users"] = 1, ["outage"] = 0, ["tested"] = 0, ["backout"] = 0, ["complexity"] = 1, ["experience"] = 0 };
        var highRisk = new Dictionary<string, int> { ["users"] = 3, ["outage"] = 2, ["tested"] = 1, ["backout"] = 1, ["complexity"] = 2, ["experience"] = 1 };

        var assess = await NewChangeAsync("Upgrade core switch firmware in data center A", ChangeType.Normal, ChangeState.Assess, networkId, adminId, Ci(0), cancellationToken);
        assess.Category = "Network";
        assess.Description = "Vendor advisory recommends firmware 17.9.4 to fix memory leaks observed on both core switches.";
        assess.PlannedStartAt = today.AddDays(6).AddHours(22);
        assess.PlannedEndAt = today.AddDays(7).AddHours(2);
        assess.ImplementationPlan = "1. Back up running configuration\n2. Upload firmware to standby switch\n3. Reload standby, verify routing\n4. Fail over and repeat on primary";
        assess.BackoutPlan = "Boot previous firmware image from flash and restore saved configuration.";
        assess.TestPlan = "Verify BGP/OSPF adjacencies, run synthetic transactions against the ERP and portal.";
        ApplyRisk(assess, lowRisk);

        var authorize = await NewChangeAsync("Migrate HR database to the new SQL Server cluster", ChangeType.Normal, ChangeState.Authorize, dbaId, adminId, Ci(1), cancellationToken);
        authorize.Category = "Database";
        authorize.Impact = Impact.High;
        authorize.Priority = PriorityMatrix.Calculate(authorize.Impact, authorize.Urgency);
        authorize.Description = "Move the HR database from the end-of-life SQL Server 2016 instance to the highly available cluster.";
        authorize.Justification = "SQL Server 2016 reaches end of extended support; the cluster provides high availability.";
        authorize.PlannedStartAt = today.AddDays(10).AddHours(20);
        authorize.PlannedEndAt = today.AddDays(11).AddHours(1);
        authorize.ImplementationPlan = "1. Freeze HR application\n2. Full backup and restore to cluster\n3. Switch connection strings\n4. Smoke test";
        authorize.BackoutPlan = "Point the application back to the old instance; it stays read-write until sign-off.";
        authorize.TestPlan = "HR key users validate payroll preview and employee search.";
        ApplyRisk(authorize, highRisk);
        authorize.Approval = ApprovalStatus.Requested;

        var standard = await NewChangeAsync("Monthly Windows patching – workstation ring 1", ChangeType.Standard, ChangeState.Scheduled, desktopId, adminId, null, cancellationToken);
        standard.Category = "Software";
        standard.PlannedStartAt = today.AddDays(3).AddHours(18);
        standard.PlannedEndAt = today.AddDays(3).AddHours(23);
        standard.ImplementationPlan = "Approve the monthly cumulative update for the ring 1 deployment group in the patch management console.";
        standard.BackoutPlan = "Uninstall the update through the patch management console.";

        var emergency = await NewChangeAsync("Emergency patch for VPN gateway vulnerability", ChangeType.Emergency, ChangeState.Implement, networkId, adminId, Ci(2), cancellationToken);
        emergency.Category = "Security";
        emergency.Impact = Impact.High;
        emergency.Urgency = Urgency.High;
        emergency.Priority = PriorityMatrix.Calculate(emergency.Impact, emergency.Urgency);
        emergency.PlannedStartAt = now.AddHours(-1);
        emergency.PlannedEndAt = now.AddHours(2);
        emergency.ActualStartAt = now.AddMinutes(-50);
        emergency.ImplementationPlan = "Apply vendor hotfix to both VPN gateways one after another.";
        emergency.BackoutPlan = "Roll back to the previous system image snapshot.";
        emergency.Approval = ApprovalStatus.Approved;

        var closed = await NewChangeAsync("Replace expiring TLS certificate on the customer portal", ChangeType.Normal, ChangeState.Closed, networkId, adminId, null, cancellationToken);
        closed.Category = "Security";
        closed.PlannedStartAt = today.AddDays(-6).AddHours(19);
        closed.PlannedEndAt = today.AddDays(-6).AddHours(20);
        closed.ActualStartAt = closed.PlannedStartAt;
        closed.ActualEndAt = closed.PlannedStartAt.Value.AddMinutes(35);
        closed.ImplementationPlan = "Install the renewed certificate on both load balancers.";
        closed.BackoutPlan = "Re-bind the previous certificate (valid for 10 more days).";
        ApplyRisk(closed, lowRisk);
        closed.Approval = ApprovalStatus.Approved;
        closed.CloseCode = ChangeCloseCode.Successful;
        closed.CloseNotes = "Certificate replaced without downtime. Monitoring confirms the new expiry date.";
        closed.ClosedAt = closed.ActualEndAt;
        closed.ClosedById = adminId;
        closed.Active = false;

        var draft = await NewChangeAsync("Decommission legacy file server FS01", ChangeType.Normal, ChangeState.New, desktopId, null, null, cancellationToken);
        draft.Category = "Hardware";
        draft.Description = "All shares were migrated to the document management system. Remove the server from the domain and recycle the hardware.";

        db.Set<ChangeRequest>().AddRange(assess, authorize, standard, emergency, closed, draft);

        db.Set<ChangeTask>().AddRange(
            await NewTaskAsync(assess, "Download firmware and verify checksum", networkId, ChangeTaskState.Completed, 10, cancellationToken),
            await NewTaskAsync(assess, "Notify service owners about the maintenance window", networkId, ChangeTaskState.Open, 20, cancellationToken),
            await NewTaskAsync(emergency, "Patch VPN gateway 1", networkId, ChangeTaskState.Completed, 10, cancellationToken),
            await NewTaskAsync(emergency, "Patch VPN gateway 2", networkId, ChangeTaskState.InProgress, 20, cancellationToken));

        var cabMembers = await db.Set<GroupMember>().Where(m => m.GroupId == cabId).Select(m => m.UserId).Take(50).ToListAsync(cancellationToken);
        var batchId = Guid.CreateVersion7();
        foreach (var member in cabMembers)
        {
            db.Set<Approval>().Add(new Approval
            {
                TableName = TableNames.ChangeRequest,
                RecordId = authorize.Id,
                RecordNumber = authorize.Number,
                RecordSummary = authorize.ShortDescription,
                RecordRoute = $"/changes/{authorize.Id}",
                BatchId = batchId,
                ApproverId = member,
                GroupId = cabId,
                Mode = ApprovalMode.AllApprovers,
                State = ApprovalRecordState.Requested,
                Reason = "Normal change · risk High · planned in 10 days",
                RequestedById = adminId,
                RequestedAt = now.AddHours(-3),
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        async Task<ChangeRequest> NewChangeAsync(string title, ChangeType type, ChangeState state, Guid groupId, Guid? assignee, Guid? ci, CancellationToken ct) => new()
        {
            Number = await numbers.NextAsync(TableNames.ChangeRequest, ChangeTables.ChangeRequestPrefix, ct),
            ShortDescription = title,
            Type = type,
            State = state,
            AssignmentGroupId = groupId,
            AssignedToId = assignee,
            RequestedById = adminId,
            OpenedById = adminId,
            OpenedAt = now.AddDays(-2),
            ConfigurationItemId = ci,
            Impact = Impact.Medium,
            Urgency = Urgency.Medium,
            Priority = PriorityMatrix.Calculate(Impact.Medium, Urgency.Medium),
        };

        async Task<ChangeTask> NewTaskAsync(ChangeRequest change, string title, Guid groupId, ChangeTaskState state, int order, CancellationToken ct) => new()
        {
            Number = await numbers.NextAsync(ChangeTables.ChangeTask, ChangeTables.ChangeTaskPrefix, ct),
            ChangeRequestId = change.Id,
            ShortDescription = title,
            AssignmentGroupId = groupId,
            State = state,
            Active = state is ChangeTaskState.Open or ChangeTaskState.InProgress,
            ClosedAt = state == ChangeTaskState.Completed ? now.AddHours(-1) : null,
            OpenedAt = now.AddDays(-1),
            OpenedById = adminId,
            Priority = change.Priority,
            Order = order,
        };
    }

    private void ApplyRisk(ChangeRequest change, Dictionary<string, int> answers)
    {
        var result = ChangeRiskCalculator.Calculate(answers)!;
        change.Risk = result.Risk;
        change.RiskScore = result.Score;
        change.RiskAssessmentJson = JsonSerializer.Serialize(answers);
        change.RiskAssessedAt = clock.UtcNow.AddDays(-1);
    }
}
