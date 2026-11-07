using System.Net;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Cmdb.Graph;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Directory;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Assets;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;
using Servora.Domain.Knowledge;

namespace Servora.Application.Cmdb;

/// <summary>Default <see cref="ICmdbService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="directory">The directory look-ups.</param>
/// <param name="activity">The activity stream.</param>
/// <param name="audit">The audit log.</param>
/// <param name="records">The generic record service (related incidents, problems and changes).</param>
/// <param name="tables">The table registry.</param>
public sealed class CmdbService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IDirectoryLookupService directory,
    IActivityService activity,
    IAuditService audit,
    IRecordService records,
    ITableRegistry tables) : ICmdbService
{
    private const int MaxRelationships = 500;
    private const int MaxEdgesPerLevel = 2000;
    private static readonly string[] s_relatedTaskTables = [TableNames.Incident, TableNames.Problem, TableNames.ChangeRequest];

    /// <inheritdoc />
    public async Task<Result<CmdbOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<CmdbOverview>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var classes = await CiClassService.LoadAsync(db, cancellationToken);
        var byStatus = await db.Set<ConfigurationItem>().GroupBy(c => c.OperationalStatus).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var byClass = await db.Set<ConfigurationItem>().GroupBy(c => c.ClassName).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var relationshipCount = await db.Set<CiRelationship>().CountAsync(cancellationToken);
        var critical = await db.Set<ConfigurationItem>().CountAsync(c => c.Criticality == 1 && c.OperationalStatus != OperationalStatus.Retired, cancellationToken);

        var classCounts = classes.Select(c => new CiClassCount(c.Key, c.Label, c.Icon, c.Color, byClass.GetValueOrDefault(c.Key))).ToList();
        classCounts.AddRange(byClass.Where(kv => classes.All(c => c.Key != kv.Key)).Select(kv => new CiClassCount(kv.Key, kv.Key, "box", "#64748b", kv.Value)));
        return Result<CmdbOverview>.Success(new CmdbOverview(byStatus.Values.Sum(), byStatus, classCounts, relationshipCount, critical));
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<CiSummary>>> ListAsync(CiQuery query, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PagedResult<CiSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var classes = await CiClassService.LoadAsync(db, cancellationToken);
        var items = db.Set<ConfigurationItem>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.ClassKey))
        {
            var keys = CiClassHierarchy.SelfAndDescendants(classes, query.ClassKey).ToList();
            items = items.Where(c => keys.Contains(c.ClassName));
        }

        if (query.Status is { } status)
        {
            items = items.Where(c => c.OperationalStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Environment))
        {
            items = items.Where(c => c.Environment == query.Environment);
        }

        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var term = query.Page.Search.Trim().ToLower();
            items = items.Where(c => c.Name.ToLower().Contains(term) || (c.Fqdn != null && c.Fqdn.ToLower().Contains(term))
                || (c.IpAddress != null && c.IpAddress.Contains(term)) || (c.Description != null && c.Description.ToLower().Contains(term)));
        }

        var desc = query.Page.SortDescending;
        items = query.Page.SortBy switch
        {
            "ClassName" => desc ? items.OrderByDescending(c => c.ClassName).ThenBy(c => c.Name) : items.OrderBy(c => c.ClassName).ThenBy(c => c.Name),
            "Status" => desc ? items.OrderByDescending(c => c.OperationalStatus) : items.OrderBy(c => c.OperationalStatus),
            "Environment" => desc ? items.OrderByDescending(c => c.Environment) : items.OrderBy(c => c.Environment),
            "Criticality" => desc ? items.OrderByDescending(c => c.Criticality) : items.OrderBy(c => c.Criticality),
            "UpdatedAt" => desc ? items.OrderByDescending(c => c.UpdatedAt) : items.OrderBy(c => c.UpdatedAt),
            "Name" when desc => items.OrderByDescending(c => c.Name),
            _ => items.OrderBy(c => c.Name),
        };

        var page = await items.ToPagedResultAsync(query.Page, cancellationToken);
        var groups = await directory.GetGroupsAsync(page.Items.Where(c => c.SupportGroupId is not null).Select(c => c.SupportGroupId!.Value), cancellationToken);
        var rows = page.Items.Select(c =>
        {
            var cls = classes.FirstOrDefault(x => x.Key == c.ClassName);
            return new CiSummary(c.Id, c.Name, c.ClassName, cls?.Label ?? c.ClassName, cls?.Icon ?? "box", cls?.Color ?? "#64748b", c.OperationalStatus,
                c.Environment, c.IpAddress, c.Fqdn, c.Location, c.Criticality,
                c.SupportGroupId is { } g ? groups.GetValueOrDefault(g)?.Display : null, c.UpdatedAt);
        }).ToList();
        return Result<PagedResult<CiSummary>>.Success(new PagedResult<CiSummary>(rows, page.TotalCount, page.Page, page.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<CiDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<CiDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ci = await db.Set<ConfigurationItem>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (ci is null)
        {
            return Result<CiDetail>.NotFound("The configuration item was not found.");
        }

        var classes = await CiClassService.LoadAsync(db, cancellationToken);
        var chain = CiClassHierarchy.Chain(classes, ci.ClassName);
        var cls = chain.Count > 0 ? chain[^1] : null;
        var owner = ci.OwnedById is { } ownerId ? (await directory.GetUsersAsync([ownerId], cancellationToken)).GetValueOrDefault(ownerId) : null;
        var group = ci.SupportGroupId is { } groupId ? (await directory.GetGroupsAsync([groupId], cancellationToken)).GetValueOrDefault(groupId) : null;

        var detail = new CiDetail
        {
            Id = ci.Id,
            Values = new CiInput
            {
                Name = ci.Name,
                ClassName = ci.ClassName,
                Description = ci.Description,
                OperationalStatus = ci.OperationalStatus,
                Environment = ci.Environment,
                OwnedById = ci.OwnedById,
                SupportGroupId = ci.SupportGroupId,
                IpAddress = ci.IpAddress,
                Fqdn = ci.Fqdn,
                SerialNumber = ci.SerialNumber,
                Manufacturer = ci.Manufacturer,
                Model = ci.Model,
                Location = ci.Location,
                Criticality = ci.Criticality,
                Attributes = CiAttributeSchema.ParseValues(ci.CustomFieldsJson),
                Version = ci.Version,
            },
            ClassLabel = cls?.Label ?? ci.ClassName,
            ClassIcon = cls?.Icon ?? "box",
            ClassColor = cls?.Color ?? "#64748b",
            ClassPath = chain.Select(c => c.Label).ToList(),
            AttributeDefinitions = CiClassHierarchy.EffectiveAttributes(classes, ci.ClassName),
            OwnerName = owner?.Display,
            SupportGroupName = group?.Display,
            DiscoverySource = ci.DiscoverySource,
            LastDiscoveredAt = ci.LastDiscoveredAt,
            CreatedAt = ci.CreatedAt,
            CreatedBy = ci.CreatedBy,
            UpdatedAt = ci.UpdatedAt,
            UpdatedBy = ci.UpdatedBy,
            UpstreamCount = await db.Set<CiRelationship>().CountAsync(r => r.ChildId == id, cancellationToken),
            DownstreamCount = await db.Set<CiRelationship>().CountAsync(r => r.ParentId == id, cancellationToken),
        };

        return Result<CiDetail>.Success(detail);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(CiInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Write, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ci = new ConfigurationItem { DiscoverySource = "Manual" };
        var error = await ApplyAsync(db, ci, input, cancellationToken);
        if (error is not null)
        {
            return Result<Guid>.Invalid(error);
        }

        db.Set<ConfigurationItem>().Add(ci);
        await db.SaveChangesAsync(cancellationToken);
        await activity.AddSystemAsync(TableNames.ConfigurationItem, ci.Id, "Configuration item created.", cancellationToken);
        return Result<Guid>.Success(ci.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, CiInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ci = await db.Set<ConfigurationItem>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (ci is null)
        {
            return Result.NotFound();
        }

        if (input.Version != 0 && input.Version != ci.Version)
        {
            return Result.Conflict("The configuration item was changed by someone else. Reload it and apply your changes again.");
        }

        var previousStatus = ci.OperationalStatus;
        var error = await ApplyAsync(db, ci, input, cancellationToken);
        if (error is not null)
        {
            return Result.Invalid(error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The configuration item was changed by someone else. Reload it and apply your changes again.");
        }

        if (previousStatus != ci.OperationalStatus)
        {
            await activity.AddSystemAsync(TableNames.ConfigurationItem, ci.Id,
                $"Operational status changed from {CmdbDisplay.Status(previousStatus)} to {CmdbDisplay.Status(ci.OperationalStatus)}.", cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ci = await db.Set<ConfigurationItem>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (ci is null)
        {
            return Result.NotFound();
        }

        var relationships = await db.Set<CiRelationship>().Where(r => r.ParentId == id || r.ChildId == id).Take(10_000).ToListAsync(cancellationToken);
        db.Set<CiRelationship>().RemoveRange(relationships);
        db.Set<KnowledgeArticleCi>().RemoveRange(await db.Set<KnowledgeArticleCi>().Where(l => l.ConfigurationItemId == id).Take(10_000).ToListAsync(cancellationToken));
        foreach (var asset in await db.Set<Asset>().Where(a => a.ConfigurationItemId == id).Take(10_000).ToListAsync(cancellationToken))
        {
            asset.ConfigurationItemId = null;
        }

        db.Set<ConfigurationItem>().Remove(ci);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Conflict("The configuration item is still referenced by other records (for example incidents or changes). Retire it instead.");
        }

        await audit.WriteAsync(new AuditEntry("cmdb", "ci.delete") { TargetType = TableNames.ConfigurationItem, TargetId = id.ToString(), TargetName = ci.Name, Details = new { relationships = relationships.Count } }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CiRelationshipDto>>> GetRelationshipsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<IReadOnlyList<CiRelationshipDto>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var relationships = await db.Set<CiRelationship>().AsNoTracking().Where(r => r.ParentId == id || r.ChildId == id).Take(MaxRelationships).ToListAsync(cancellationToken);
        var ids = relationships.SelectMany(r => new[] { r.ParentId, r.ChildId }).Distinct().ToList();
        var cis = await db.Set<ConfigurationItem>().AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Name, c.ClassName }).ToDictionaryAsync(c => c.Id, cancellationToken);
        var classes = (await CiClassService.LoadAsync(db, cancellationToken)).ToDictionary(c => c.Key, c => c.Label);
        var result = relationships
            .Where(r => cis.ContainsKey(r.ParentId) && cis.ContainsKey(r.ChildId))
            .Select(r => new CiRelationshipDto(
                r.Id,
                r.ParentId,
                cis[r.ParentId].Name,
                classes.GetValueOrDefault(cis[r.ParentId].ClassName) ?? cis[r.ParentId].ClassName,
                r.ChildId,
                cis[r.ChildId].Name,
                classes.GetValueOrDefault(cis[r.ChildId].ClassName) ?? cis[r.ChildId].ClassName,
                r.Type,
                r.Note))
            .OrderBy(r => r.ParentId == id ? 1 : 0)
            .ThenBy(r => r.Type)
            .ThenBy(r => r.ParentId == id ? r.ChildName : r.ParentName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Result<IReadOnlyList<CiRelationshipDto>>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> AddRelationshipAsync(CiRelationshipInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Write, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (input.ParentId == input.ChildId)
        {
            return Result<Guid>.Invalid("A configuration item cannot be related to itself.");
        }

        if (!Enum.IsDefined(input.Type))
        {
            return Result<Guid>.Invalid("Unknown relationship type.");
        }

        if (input.Note?.Length > 500)
        {
            return Result<Guid>.Invalid("The note must not exceed 500 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var names = await db.Set<ConfigurationItem>().AsNoTracking().Where(c => c.Id == input.ParentId || c.Id == input.ChildId)
            .Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        if (names.Count != 2)
        {
            return Result<Guid>.Invalid("Both configuration items must exist.");
        }

        if (await db.Set<CiRelationship>().AnyAsync(r => r.ParentId == input.ParentId && r.ChildId == input.ChildId && r.Type == input.Type, cancellationToken))
        {
            return Result<Guid>.Conflict("This relationship already exists.");
        }

        var relationship = new CiRelationship { ParentId = input.ParentId, ChildId = input.ChildId, Type = input.Type, Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim() };
        db.Set<CiRelationship>().Add(relationship);
        await db.SaveChangesAsync(cancellationToken);
        var text = $"Relationship added: {names[input.ParentId]} {CiRelationshipTypes.Forward(input.Type).ToLowerInvariant()} {names[input.ChildId]}.";
        await activity.AddSystemAsync(TableNames.ConfigurationItem, input.ParentId, text, cancellationToken);
        await activity.AddSystemAsync(TableNames.ConfigurationItem, input.ChildId, text, cancellationToken);
        return Result<Guid>.Success(relationship.Id);
    }

    /// <inheritdoc />
    public async Task<Result> RemoveRelationshipAsync(Guid relationshipId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var relationship = await db.Set<CiRelationship>().FirstOrDefaultAsync(r => r.Id == relationshipId, cancellationToken);
        if (relationship is null)
        {
            return Result.NotFound();
        }

        var names = await db.Set<ConfigurationItem>().AsNoTracking().Where(c => c.Id == relationship.ParentId || c.Id == relationship.ChildId)
            .Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        db.Set<CiRelationship>().Remove(relationship);
        await db.SaveChangesAsync(cancellationToken);
        var text = $"Relationship removed: {names.GetValueOrDefault(relationship.ParentId)} {CiRelationshipTypes.Forward(relationship.Type).ToLowerInvariant()} {names.GetValueOrDefault(relationship.ChildId)}.";
        await activity.AddSystemAsync(TableNames.ConfigurationItem, relationship.ParentId, text, cancellationToken);
        await activity.AddSystemAsync(TableNames.ConfigurationItem, relationship.ChildId, text, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<CiGraph>> GetGraphAsync(Guid id, int depth = 2, CiGraphDirection direction = CiGraphDirection.Both, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<CiGraph>.Forbidden();
        }

        depth = Math.Clamp(depth, 1, CiGraphBuilder.MaxDepth);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Set<ConfigurationItem>().AnyAsync(c => c.Id == id, cancellationToken))
        {
            return Result<CiGraph>.NotFound("The configuration item was not found.");
        }

        // Fetch the neighbourhood level by level (bounded), then let the pure builder compute levels and limits.
        var edges = new Dictionary<Guid, CiGraphEdge>();
        var seen = new HashSet<Guid> { id };
        var downFrontier = direction == CiGraphDirection.Upstream ? new List<Guid>() : [id];
        var upFrontier = direction == CiGraphDirection.Downstream ? new List<Guid>() : [id];
        for (var level = 0; level < depth && (downFrontier.Count > 0 || upFrontier.Count > 0) && seen.Count <= CiGraphBuilder.DefaultMaxNodes * 2; level++)
        {
            var down = downFrontier;
            var up = upFrontier;
            var fetched = await db.Set<CiRelationship>().AsNoTracking()
                .Where(r => down.Contains(r.ParentId) || up.Contains(r.ChildId))
                .Take(MaxEdgesPerLevel)
                .Select(r => new CiGraphEdge(r.Id, r.ParentId, r.ChildId, r.Type))
                .ToListAsync(cancellationToken);
            var nextDown = new List<Guid>();
            var nextUp = new List<Guid>();
            foreach (var edge in fetched)
            {
                edges.TryAdd(edge.Id, edge);
                if (down.Contains(edge.ParentId) && seen.Add(edge.ChildId))
                {
                    nextDown.Add(edge.ChildId);
                }

                if (up.Contains(edge.ChildId) && seen.Add(edge.ParentId))
                {
                    nextUp.Add(edge.ParentId);
                }
            }

            downFrontier = nextDown;
            upFrontier = nextUp;
        }

        // Edges between already discovered nodes (cross links) make the picture complete.
        var seenIds = seen.ToList();
        var cross = await db.Set<CiRelationship>().AsNoTracking()
            .Where(r => seenIds.Contains(r.ParentId) && seenIds.Contains(r.ChildId))
            .Take(MaxEdgesPerLevel)
            .Select(r => new CiGraphEdge(r.Id, r.ParentId, r.ChildId, r.Type))
            .ToListAsync(cancellationToken);
        foreach (var edge in cross)
        {
            edges.TryAdd(edge.Id, edge);
        }

        var traversal = CiGraphBuilder.Traverse(id, edges.Values, depth, direction);
        var nodeIds = traversal.Levels.Keys.ToList();
        var cis = await db.Set<ConfigurationItem>().AsNoTracking().Where(c => nodeIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.ClassName, c.OperationalStatus }).ToListAsync(cancellationToken);
        var classes = (await CiClassService.LoadAsync(db, cancellationToken)).ToDictionary(c => c.Key);
        var nodes = cis.Select(c =>
        {
            var cls = classes.GetValueOrDefault(c.ClassName);
            return new CiGraphNode(c.Id, c.Name, c.ClassName, cls?.Label ?? c.ClassName, cls?.Icon ?? "box", cls?.Color ?? "#64748b", c.OperationalStatus, traversal.Levels[c.Id]);
        }).ToList();
        return Result<CiGraph>.Success(new CiGraph(id, nodes, traversal.Edges, traversal.Truncated));
    }

    /// <inheritdoc />
    public async Task<Result<CiRelatedRecords>> GetRelatedRecordsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<CiRelatedRecords>.Forbidden();
        }

        var result = new List<CiRelatedRecord>();
        var unavailable = new List<string>();

        // Task records of other modules through the generic record service, which enforces their permissions.
        foreach (var tableName in s_relatedTaskTables)
        {
            var table = await tables.GetTableAsync(tableName, cancellationToken);
            if (table is null)
            {
                continue;
            }

            var query = new RecordQuery
            {
                Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "ConfigurationItemId", Operator = ConditionOperator.Equals, Value = id.ToString() }] },
                Page = new PageRequest { PageSize = 25, SortBy = "OpenedAt", SortDescending = true },
                Fields = ["Id", "Number", "ShortDescription", "State"],
            };
            var page = await records.QueryAsync(tableName, query, cancellationToken);
            if (!page.Succeeded || page.Value is null)
            {
                unavailable.Add(table.PluralLabel);
                continue;
            }

            foreach (var row in page.Value.Items)
            {
                if (row.GetValueOrDefault("Id") is not { } rawId || !Guid.TryParse(rawId.ToString(), out var recordId))
                {
                    continue;
                }

                var state = row.GetValueOrDefault("State.display") ?? row.GetValueOrDefault("State");
                result.Add(new CiRelatedRecord(tableName, table.Label, table.Icon, recordId, row.GetValueOrDefault("Number")?.ToString() ?? string.Empty,
                    row.GetValueOrDefault("ShortDescription")?.ToString() ?? string.Empty, state?.ToString(), $"{table.Route ?? "/" + tableName}/{recordId}"));
            }
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await currentUser.HasPermissionAsync(Permissions.Knowledge.Read, cancellationToken))
        {
            var seeAll = await currentUser.HasPermissionAsync(Permissions.Knowledge.Write, cancellationToken) || await currentUser.HasPermissionAsync(Permissions.Knowledge.Publish, cancellationToken);
            var articleIds = db.Set<KnowledgeArticleCi>().Where(l => l.ConfigurationItemId == id).Select(l => l.ArticleId);
            var articles = await db.Set<KnowledgeArticle>().AsNoTracking()
                .Where(a => articleIds.Contains(a.Id) && (seeAll || a.State == KnowledgeArticleState.Published))
                .OrderBy(a => a.Title).Take(25)
                .Select(a => new { a.Id, a.Number, a.Title, a.State }).ToListAsync(cancellationToken);
            result.AddRange(articles.Select(a => new CiRelatedRecord(TableNames.KnowledgeArticle, "Knowledge Article", "book-open", a.Id, a.Number, a.Title, KnowledgeWorkflow.Label(a.State), $"/knowledge/{a.Id}")));
        }

        if (await currentUser.HasPermissionAsync(Permissions.Assets.Read, cancellationToken))
        {
            var assets = await db.Set<Asset>().AsNoTracking().Where(a => a.ConfigurationItemId == id).OrderBy(a => a.Number).Take(25)
                .Select(a => new { a.Id, a.Number, a.DisplayName, a.State }).ToListAsync(cancellationToken);
            result.AddRange(assets.Select(a => new CiRelatedRecord(TableNames.Asset, "Asset", "box", a.Id, a.Number, a.DisplayName, AssetLifecycle.Label(a.State), $"/assets/{a.Id}")));
        }

        return Result<CiRelatedRecords>.Success(new CiRelatedRecords(result, unavailable));
    }

    private static string? Clean(string? value, int max, string label, out string? error)
    {
        error = null;
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            error = $"{label} must not exceed {max} characters.";
        }

        return trimmed;
    }

    private async Task<string?> ApplyAsync(IAppDbContext db, ConfigurationItem ci, CiInput input, CancellationToken cancellationToken)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 255)
        {
            return "The name is required (max 255 characters).";
        }

        var classes = await CiClassService.LoadAsync(db, cancellationToken);
        if (classes.All(c => c.Key != input.ClassName))
        {
            return "Select a valid CI class.";
        }

        if (!Enum.IsDefined(input.OperationalStatus))
        {
            return "Select a valid operational status.";
        }

        if (input.Criticality is < 1 or > 4)
        {
            return "The criticality must be between 1 and 4.";
        }

        var ip = Clean(input.IpAddress, 64, "The IP address", out var error);
        if (error is null && ip is not null && !IPAddress.TryParse(ip, out _))
        {
            error = "The IP address is not valid.";
        }

        var description = Clean(input.Description, 4000, "The description", out var e1);
        var environment = Clean(input.Environment, 64, "The environment", out var e2);
        var fqdn = Clean(input.Fqdn, 255, "The FQDN", out var e3);
        var serial = Clean(input.SerialNumber, 128, "The serial number", out var e4);
        var manufacturer = Clean(input.Manufacturer, 128, "The manufacturer", out var e5);
        var model = Clean(input.Model, 128, "The model", out var e6);
        var location = Clean(input.Location, 128, "The location", out var e7);
        error ??= e1 ?? e2 ?? e3 ?? e4 ?? e5 ?? e6 ?? e7;
        if (error is not null)
        {
            return error;
        }

        var definitions = CiClassHierarchy.EffectiveAttributes(classes, input.ClassName);
        var attributeError = CiAttributeSchema.ApplyValues(definitions, input.Attributes, ci.CustomFieldsJson, out var customJson);
        if (attributeError is not null)
        {
            return attributeError;
        }

        ci.Name = name;
        ci.ClassName = input.ClassName;
        ci.Description = description;
        ci.OperationalStatus = input.OperationalStatus;
        ci.Environment = environment;
        ci.OwnedById = input.OwnedById;
        ci.SupportGroupId = input.SupportGroupId;
        ci.IpAddress = ip;
        ci.Fqdn = fqdn;
        ci.SerialNumber = serial;
        ci.Manufacturer = manufacturer;
        ci.Model = model;
        ci.Location = location;
        ci.Criticality = input.Criticality;
        ci.CustomFieldsJson = customJson;
        return null;
    }

    private Task<bool> CanReadAsync(CancellationToken cancellationToken) => currentUser.HasPermissionAsync(Permissions.Cmdb.Read, cancellationToken);
}
