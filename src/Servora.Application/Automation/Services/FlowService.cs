using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Scheduling;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>Default <see cref="IFlowService"/>.</summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="registry">The action registry.</param>
/// <param name="starter">The run starter.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="records">The record service (test runs).</param>
/// <param name="tables">The table registry.</param>
/// <param name="audit">The audit service.</param>
/// <param name="cache">The memory cache.</param>
/// <param name="clock">The clock.</param>
public sealed class FlowService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    FlowActionRegistry registry,
    IFlowRunStarter starter,
    ISecretStore secrets,
    IRecordService records,
    ITableRegistry tables,
    IAuditService audit,
    IMemoryCache cache,
    IClock clock) : IFlowService
{
    private const string AuditCategory = "automation";

    /// <inheritdoc />
    public async Task<Result<PagedResult<FlowListItem>>> ListAsync(FlowListQuery query, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken))
        {
            return Result<PagedResult<FlowListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flows = db.Set<Flow>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var search = query.Page.Search.Trim();
            flows = flows.Where(f => f.Name.Contains(search) || (f.Description != null && f.Description.Contains(search)) || (f.TriggerTable != null && f.TriggerTable.Contains(search)));
        }

        if (query.Active is { } active)
        {
            flows = flows.Where(f => f.IsActive == active);
        }

        if (query.Trigger is { } trigger)
        {
            flows = flows.Where(f => f.TriggerType == trigger);
        }

        flows = (query.Page.SortBy, query.Page.SortDescending) switch
        {
            ("UpdatedAt", true) => flows.OrderByDescending(f => f.UpdatedAt),
            ("UpdatedAt", false) => flows.OrderBy(f => f.UpdatedAt),
            ("Trigger", true) => flows.OrderByDescending(f => f.TriggerType).ThenBy(f => f.Name),
            ("Trigger", false) => flows.OrderBy(f => f.TriggerType).ThenBy(f => f.Name),
            (_, true) => flows.OrderByDescending(f => f.Name),
            _ => flows.OrderBy(f => f.Name),
        };

        var page = await flows
            .Select(f => new FlowListItem(f.Id, f.Name, f.Description, f.IsActive, f.TriggerType, f.TriggerTable, f.CronExpression, f.DefinitionVersion, f.UpdatedAt, f.UpdatedBy, f.NextRunAt))
            .ToPagedResultAsync(query.Page, cancellationToken);

        var since = clock.UtcNow.AddHours(-24);
        var counts = await db.Set<FlowRun>().AsNoTracking()
            .Where(r => r.QueuedAt >= since)
            .GroupBy(r => r.FlowId)
            .Select(g => new { FlowId = g.Key, Count = g.Count() })
            .Take(1000)
            .ToListAsync(cancellationToken);
        var items = new List<FlowListItem>(page.Items.Count);
        foreach (var item in page.Items)
        {
            var last = await db.Set<FlowRun>().AsNoTracking()
                .Where(r => r.FlowId == item.Id)
                .OrderByDescending(r => r.QueuedAt)
                .Select(r => new { r.QueuedAt, r.Status })
                .FirstOrDefaultAsync(cancellationToken);
            items.Add(item with
            {
                LastRunAt = last?.QueuedAt,
                LastRunStatus = last?.Status,
                Runs24h = counts.FirstOrDefault(c => c.FlowId == item.Id)?.Count ?? 0,
            });
        }

        return Result<PagedResult<FlowListItem>>.Success(page with { Items = items });
    }

    /// <inheritdoc />
    public async Task<Result<FlowOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken))
        {
            return Result<FlowOverview>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var total = await db.Set<Flow>().CountAsync(cancellationToken);
        var activeCount = await db.Set<Flow>().CountAsync(f => f.IsActive, cancellationToken);
        var now = clock.UtcNow;
        var since = now.AddHours(-24);
        var runs = await db.Set<FlowRun>().AsNoTracking()
            .Where(r => r.QueuedAt >= since)
            .OrderByDescending(r => r.QueuedAt)
            .Select(r => new { r.QueuedAt, r.Status, r.StartedAt, r.CompletedAt })
            .Take(20_000)
            .ToListAsync(cancellationToken);
        var waiting = await db.Set<FlowRun>().CountAsync(r => r.Status == FlowRunStatus.Waiting, cancellationToken);
        var running = await db.Set<FlowRun>().CountAsync(r => r.Status == FlowRunStatus.Running || r.Status == FlowRunStatus.Queued, cancellationToken);
        var completed = runs.Where(r => r.Status is FlowRunStatus.Succeeded or FlowRunStatus.Failed).ToList();
        var successRate = completed.Count == 0 ? 100 : Math.Round(100.0 * completed.Count(r => r.Status == FlowRunStatus.Succeeded) / completed.Count, 1);
        var durations = completed.Where(r => r.StartedAt is not null && r.CompletedAt is not null).Select(r => (r.CompletedAt!.Value - r.StartedAt!.Value).TotalMilliseconds).ToList();
        var hourly = Enumerable.Range(0, 24)
            .Select(i => (double)runs.Count(r => r.QueuedAt >= since.AddHours(i) && r.QueuedAt < since.AddHours(i + 1)))
            .ToList();
        return Result<FlowOverview>.Success(new FlowOverview(
            total,
            activeCount,
            runs.Count,
            runs.Count(r => r.Status == FlowRunStatus.Failed),
            waiting,
            running,
            successRate,
            durations.Count == 0 ? 0 : (long)durations.Average(),
            hourly));
    }

    /// <inheritdoc />
    public async Task<Result<FlowDefinitionModel>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken))
        {
            return Result<FlowDefinitionModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flow = await db.Set<Flow>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (flow is null)
        {
            return Result<FlowDefinitionModel>.NotFound("The flow was not found.");
        }

        var hasSecret = await secrets.ExistsAsync(FlowSecretNames.Webhook(id), cancellationToken);
        return Result<FlowDefinitionModel>.Success(ToModel(flow, hasSecret));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(FlowCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Manage, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 200)
        {
            return Result<Guid>.Invalid("Enter a name of up to 200 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Set<Flow>().AnyAsync(f => f.Name == name, cancellationToken))
        {
            return Result<Guid>.Conflict($"A flow named '{name}' already exists.");
        }

        var flow = new Flow
        {
            Name = name,
            Description = FlowRedactor.Truncate(request.Description?.Trim(), 2000),
            TriggerType = request.TriggerType,
            TriggerTable = IsRecordTrigger(request.TriggerType) ? request.TriggerTable?.Trim() : null,
            CronExpression = request.TriggerType is FlowTriggerType.Schedule or FlowTriggerType.DataSourcePoll ? "0 8 * * 1-5" : null,
            StepsJson = "[]",
            DefinitionVersion = 1,
            VersionNote = "Created",
        };
        db.Set<Flow>().Add(flow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "flow.created") { TargetType = "flow", TargetId = flow.Id.ToString(), TargetName = flow.Name }, cancellationToken);
        return Result<Guid>.Success(flow.Id);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(FlowDefinitionModel model, bool forActivation)
    {
        FlowDefinitionValidator.Normalize(model, registry);
        return FlowDefinitionValidator.Validate(model, registry, forActivation);
    }

    /// <inheritdoc />
    public async Task<Result<FlowDefinitionModel>> SaveAsync(FlowDefinitionModel model, string? versionNote, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Manage, cancellationToken))
        {
            return Result<FlowDefinitionModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flow = await db.Set<Flow>().FirstOrDefaultAsync(f => f.Id == model.Id, cancellationToken);
        if (flow is null)
        {
            return Result<FlowDefinitionModel>.NotFound("The flow was not found.");
        }

        if (flow.Version != model.ConcurrencyVersion)
        {
            return Result<FlowDefinitionModel>.Conflict("The flow was changed by someone else. Reload it and apply your changes again.");
        }

        FlowDefinitionValidator.Normalize(model, registry);
        var issues = FlowDefinitionValidator.Validate(model, registry, forActivation: flow.IsActive);
        if (issues.Count > 0)
        {
            return Result<FlowDefinitionModel>.Invalid(string.Join(" ", issues.Take(5)));
        }

        var name = model.Name.Trim();
        if (await db.Set<Flow>().AnyAsync(f => f.Name == name && f.Id != flow.Id, cancellationToken))
        {
            return Result<FlowDefinitionModel>.Conflict($"A flow named '{name}' already exists.");
        }

        if (IsRecordTrigger(model.TriggerType) && !string.IsNullOrWhiteSpace(model.TriggerTable) && await tables.GetTableAsync(model.TriggerTable.Trim(), cancellationToken) is null)
        {
            return Result<FlowDefinitionModel>.Invalid($"The table '{model.TriggerTable}' does not exist.");
        }

        var stepsJson = FlowJson.Serialize(model.Steps);
        if (stepsJson.Length > 500_000)
        {
            return Result<FlowDefinitionModel>.Invalid("The flow definition is too large.");
        }

        var timed = model.TriggerType is FlowTriggerType.Schedule or FlowTriggerType.DataSourcePoll;
        flow.Name = name;
        flow.Description = FlowRedactor.Truncate(model.Description?.Trim(), 2000);
        flow.TriggerType = model.TriggerType;
        flow.TriggerTable = IsRecordTrigger(model.TriggerType) ? model.TriggerTable?.Trim() : null;
        flow.TriggerConditionJson = IsRecordTrigger(model.TriggerType) ? ConditionSerializer.Serialize(model.TriggerCondition) : null;
        flow.CronExpression = timed ? model.CronExpression?.Trim() : null;
        flow.TimeZoneId = timed ? NullIfEmpty(model.TimeZoneId) : null;
        flow.PollDataSourceId = model.TriggerType == FlowTriggerType.DataSourcePoll ? model.PollDataSourceId : null;
        flow.PollEndpointKey = model.TriggerType == FlowTriggerType.DataSourcePoll ? NullIfEmpty(model.PollEndpointKey) : null;
        flow.PollParametersJson = model.TriggerType == FlowTriggerType.DataSourcePoll && model.PollParameters.Count > 0 ? JsonSerializer.Serialize(model.PollParameters) : null;
        flow.PollOnlyOnChange = model.PollOnlyOnChange;
        flow.InputsJson = model.Inputs.Count > 0 ? FlowJson.Serialize(model.Inputs) : null;
        flow.StepsJson = stepsJson;
        flow.DefinitionVersion++;
        flow.VersionNote = FlowRedactor.Truncate(string.IsNullOrWhiteSpace(versionNote) ? $"Version {flow.DefinitionVersion}" : versionNote.Trim(), 500);
        flow.NextRunAt = ComputeNextRun(flow, clock.UtcNow);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<FlowDefinitionModel>.Conflict("The flow was changed by someone else. Reload it and apply your changes again.");
        }

        cache.Remove(RecordTriggerHandler.CacheKey);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "flow.updated") { TargetType = "flow", TargetId = flow.Id.ToString(), TargetName = flow.Name, Message = $"Version {flow.DefinitionVersion}: {flow.VersionNote}" }, cancellationToken);
        var hasSecret = await secrets.ExistsAsync(FlowSecretNames.Webhook(flow.Id), cancellationToken);
        return Result<FlowDefinitionModel>.Success(ToModel(flow, hasSecret));
    }

    /// <inheritdoc />
    public async Task<Result> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flow = await db.Set<Flow>().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (flow is null)
        {
            return Result.NotFound("The flow was not found.");
        }

        if (active)
        {
            var model = ToModel(flow, hasSecret: false);
            var issues = FlowDefinitionValidator.Validate(model, registry, forActivation: true).ToList();
            if (flow.TriggerType == FlowTriggerType.Webhook && !await secrets.ExistsAsync(FlowSecretNames.Webhook(id), cancellationToken))
            {
                issues.Add("Generate a webhook secret before activating the webhook trigger.");
            }

            if (issues.Count > 0)
            {
                return Result.Invalid("The flow cannot be activated: " + string.Join(" ", issues.Take(5)));
            }
        }

        flow.IsActive = active;
        flow.NextRunAt = ComputeNextRun(flow, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(RecordTriggerHandler.CacheKey);
        await audit.WriteAsync(new AuditEntry(AuditCategory, active ? "flow.activated" : "flow.deactivated") { TargetType = "flow", TargetId = flow.Id.ToString(), TargetName = flow.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> DuplicateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Manage, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Set<Flow>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (source is null)
        {
            return Result<Guid>.NotFound("The flow was not found.");
        }

        var name = FlowRedactor.Truncate($"{source.Name} (copy)", 200)!;
        for (var i = 2; await db.Set<Flow>().AnyAsync(f => f.Name == name, cancellationToken); i++)
        {
            name = FlowRedactor.Truncate($"{source.Name} (copy {i})", 200)!;
        }

        var copy = new Flow
        {
            Name = name,
            Description = source.Description,
            IsActive = false,
            TriggerType = source.TriggerType,
            TriggerTable = source.TriggerTable,
            TriggerConditionJson = source.TriggerConditionJson,
            CronExpression = source.CronExpression,
            TimeZoneId = source.TimeZoneId,
            PollDataSourceId = source.PollDataSourceId,
            PollEndpointKey = source.PollEndpointKey,
            PollParametersJson = source.PollParametersJson,
            PollOnlyOnChange = source.PollOnlyOnChange,
            InputsJson = source.InputsJson,
            StepsJson = source.StepsJson,
            DefinitionVersion = 1,
            VersionNote = $"Copied from {source.Name}",
        };
        db.Set<Flow>().Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "flow.created") { TargetType = "flow", TargetId = copy.Id.ToString(), TargetName = copy.Name, Message = $"Duplicated from {source.Name}" }, cancellationToken);
        return Result<Guid>.Success(copy.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flow = await db.Set<Flow>().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (flow is null)
        {
            return Result.NotFound("The flow was not found.");
        }

        db.Set<Flow>().Remove(flow);
        await db.SaveChangesAsync(cancellationToken);
        await secrets.DeleteAsync(FlowSecretNames.Webhook(id), cancellationToken);
        cache.Remove(RecordTriggerHandler.CacheKey);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "flow.deleted") { TargetType = "flow", TargetId = id.ToString(), TargetName = flow.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<string>> RotateWebhookSecretAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Manage, cancellationToken))
        {
            return Result<string>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flow = await db.Set<Flow>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (flow is null)
        {
            return Result<string>.NotFound("The flow was not found.");
        }

        var secret = "whsec_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        await secrets.SetAsync(FlowSecretNames.Webhook(id), secret, $"Inbound webhook secret of flow '{flow.Name}'", cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "flow.webhook_secret.rotated") { TargetType = "flow", TargetId = id.ToString(), TargetName = flow.Name }, cancellationToken);
        return Result<string>.Success(secret);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> StartTestRunAsync(Guid id, FlowTestRequest request, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Manage, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        Flow? flow;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            flow = await db.Set<Flow>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }

        if (flow is null)
        {
            return Result<Guid>.NotFound("The flow was not found.");
        }

        var inputs = FlowInputValidator.Validate(FlowJson.DeserializeInputs(flow.InputsJson), request.Inputs.ToDictionary(p => p.Key, p => (object?)p.Value));
        if (!inputs.Succeeded)
        {
            return Result<Guid>.From(inputs);
        }

        var now = clock.UtcNow;
        var trigger = new JsonObject { ["test"] = true, ["firedAt"] = now.ToString("O", CultureInfo.InvariantCulture) };
        string summary;
        if (IsRecordTrigger(flow.TriggerType) && flow.TriggerTable is { } table)
        {
            trigger["table"] = table;
            trigger["changeType"] = "Test";
            trigger["changes"] = new JsonArray();
            trigger["record"] = new JsonObject();
            summary = $"Test run on {table}";
            if (request.RecordId is { } recordId)
            {
                trigger["recordId"] = recordId.ToString();
                var record = await records.GetAsync(table, recordId, cancellationToken);
                if (record.Succeeded)
                {
                    trigger["record"] = RecordTriggerHandler.BuildRecord(record.Value!, await tables.GetTableAsync(table, cancellationToken));
                }
                else
                {
                    trigger["recordWarning"] = record.Error;
                }

                summary = $"Test run on {table} {recordId}";
            }
        }
        else
        {
            summary = "Test run";
            if (!string.IsNullOrWhiteSpace(request.PayloadJson))
            {
                JsonNode? payload;
                try
                {
                    payload = JsonNode.Parse(request.PayloadJson, documentOptions: new JsonDocumentOptions { MaxDepth = 32 });
                }
                catch (JsonException)
                {
                    return Result<Guid>.Invalid("The sample payload is not valid JSON.");
                }

                if (flow.TriggerType == FlowTriggerType.DataSourcePoll)
                {
                    trigger["response"] = new JsonObject { ["statusCode"] = 200, ["json"] = payload, ["body"] = request.PayloadJson };
                }
                else
                {
                    trigger["body"] = payload;
                }
            }

            if (flow.TriggerType == FlowTriggerType.Schedule)
            {
                trigger["scheduledAt"] = now.ToString("O", CultureInfo.InvariantCulture);
            }
        }

        return await starter.StartAsync(
            new FlowStartRequest
            {
                FlowId = id,
                TriggerType = flow.TriggerType,
                Trigger = trigger,
                Inputs = inputs.Value,
                Summary = summary,
                TriggerTable = flow.TriggerTable,
                TriggerRecordId = request.RecordId,
                IsTest = true,
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<FlowActionDescriptor> GetActions() => registry.Descriptors;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FlowSummary>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Automation.Read, cancellationToken))
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<Flow>().AsNoTracking().OrderBy(f => f.Name).Take(500)
            .Select(f => new FlowSummary(f.Id, f.Name, f.Description, f.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Computes the next fire time of a time-based trigger.</summary>
    /// <param name="flow">The flow.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The next fire time, or <see langword="null"/>.</returns>
    public static DateTimeOffset? ComputeNextRun(Flow flow, DateTimeOffset now)
    {
        if (!flow.IsActive || flow.TriggerType is not (FlowTriggerType.Schedule or FlowTriggerType.DataSourcePoll)
            || !CronExpression.TryParse(flow.CronExpression, out var cron, out _))
        {
            return null;
        }

        CronExpression.TryFindTimeZone(flow.TimeZoneId, out var zone);
        return cron!.GetNextOccurrence(now, zone);
    }

    private static bool IsRecordTrigger(FlowTriggerType type) => type is FlowTriggerType.RecordCreated or FlowTriggerType.RecordUpdated or FlowTriggerType.RecordCreatedOrUpdated;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FlowDefinitionModel ToModel(Flow flow, bool hasSecret) => new()
    {
        Id = flow.Id,
        Name = flow.Name,
        Description = flow.Description,
        IsActive = flow.IsActive,
        TriggerType = flow.TriggerType,
        TriggerTable = flow.TriggerTable,
        TriggerCondition = ConditionSerializer.Deserialize(flow.TriggerConditionJson),
        CronExpression = flow.CronExpression,
        TimeZoneId = flow.TimeZoneId,
        PollDataSourceId = flow.PollDataSourceId,
        PollEndpointKey = flow.PollEndpointKey,
        PollParameters = FlowJson.TryDeserialize<Dictionary<string, string>>(flow.PollParametersJson) ?? [],
        PollOnlyOnChange = flow.PollOnlyOnChange,
        Inputs = FlowJson.DeserializeInputs(flow.InputsJson),
        Steps = FlowJson.DeserializeSteps(flow.StepsJson),
        DefinitionVersion = flow.DefinitionVersion,
        VersionNote = flow.VersionNote,
        ConcurrencyVersion = flow.Version,
        NextRunAt = flow.NextRunAt,
        LastTriggeredAt = flow.LastTriggeredAt,
        UpdatedAt = flow.UpdatedAt,
        UpdatedBy = flow.UpdatedBy,
        HasWebhookSecret = hasSecret,
    };
}
