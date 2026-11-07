using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Services;
using Servora.Application.Common;
using Servora.Application.Integration.Imports;
using Servora.Application.Security;
using Servora.Domain.Automation;
using Servora.Domain.Integration;

namespace Servora.Application.Automation.Scheduling;

/// <summary>Default <see cref="IScheduleService"/>.</summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="flows">The flow service (activation).</param>
/// <param name="starter">The run starter.</param>
/// <param name="imports">The import set service.</param>
/// <param name="clock">The clock.</param>
public sealed class ScheduleService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IFlowService flows,
    IFlowRunStarter starter,
    IImportSetService imports,
    IClock clock) : IScheduleService
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ScheduleItem>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken))
        {
            return Result<IReadOnlyList<ScheduleItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flowItems = await db.Set<Flow>().AsNoTracking()
            .Where(f => f.TriggerType == FlowTriggerType.Schedule || f.TriggerType == FlowTriggerType.DataSourcePoll)
            .OrderBy(f => f.Name)
            .Take(500)
            .Select(f => new ScheduleItem(
                f.TriggerType == FlowTriggerType.Schedule ? ScheduleKind.Flow : ScheduleKind.DataSourcePoll,
                f.Id, f.Name, f.CronExpression, f.TimeZoneId, f.IsActive, f.NextRunAt, f.LastTriggeredAt, "/automation/flows/" + f.Id))
            .ToListAsync(cancellationToken);
        var items = new List<ScheduleItem>(flowItems);
        if (await currentUser.HasPermissionAsync(Permissions.Integration.ManageImports, cancellationToken))
        {
            var importItems = await db.Set<ImportSet>().AsNoTracking()
                .Where(s => s.CronExpression != null && s.SourceType == ImportSourceType.DataSource)
                .OrderBy(s => s.Name)
                .Take(500)
                .Select(s => new ScheduleItem(ScheduleKind.Import, s.Id, s.Name, s.CronExpression, s.TimeZoneId, s.ScheduleEnabled, s.NextRunAt, s.LastRunAt, "/integration/imports/" + s.Id))
                .ToListAsync(cancellationToken);
            items.AddRange(importItems);
        }

        return Result<IReadOnlyList<ScheduleItem>>.Success(items
            .OrderBy(i => i.Enabled ? 0 : 1)
            .ThenBy(i => i.NextRunAt ?? DateTimeOffset.MaxValue)
            .ThenBy(i => i.Name)
            .ToList());
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<DateTimeOffset>> Preview(string? cronExpression, string? timeZoneId, int count = 5)
    {
        if (!CronExpression.TryParse(cronExpression, out var cron, out var error))
        {
            return Result<IReadOnlyList<DateTimeOffset>>.Invalid(error!);
        }

        if (!CronExpression.TryFindTimeZone(timeZoneId, out var zone))
        {
            return Result<IReadOnlyList<DateTimeOffset>>.Invalid($"The time zone '{timeZoneId}' is unknown.");
        }

        return Result<IReadOnlyList<DateTimeOffset>>.Success(cron!.GetNextOccurrences(clock.UtcNow, Math.Clamp(count, 1, 20), zone));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> RunNowAsync(ScheduleKind kind, Guid id, CancellationToken cancellationToken = default)
    {
        if (kind == ScheduleKind.Import)
        {
            return await imports.RunNowAsync(id, cancellationToken);
        }

        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Execute, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flow = await db.Set<Flow>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (flow is null)
        {
            return Result<Guid>.NotFound("The flow was not found.");
        }

        var now = clock.UtcNow;
        return await starter.StartAsync(
            new FlowStartRequest
            {
                FlowId = id,
                TriggerType = flow.TriggerType,
                Trigger = new JsonObject { ["scheduledAt"] = now.ToString("O", CultureInfo.InvariantCulture), ["manual"] = true, ["startedBy"] = currentUser.DisplayName },
                Summary = $"Run now by {currentUser.DisplayName}",
                IsTest = !flow.IsActive,
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> SetEnabledAsync(ScheduleKind kind, Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        if (kind != ScheduleKind.Import)
        {
            return await flows.SetActiveAsync(id, enabled, cancellationToken);
        }

        var current = await imports.GetAsync(id, cancellationToken);
        if (!current.Succeeded)
        {
            return current;
        }

        current.Value!.ScheduleEnabled = enabled;
        var saved = await imports.SaveAsync(current.Value, cancellationToken);
        return saved.Succeeded ? Result.Success() : saved;
    }
}
