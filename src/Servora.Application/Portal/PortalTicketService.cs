using Microsoft.EntityFrameworkCore;
using Servora.Application.Approvals;
using Servora.Application.Catalog;
using Servora.Application.Common;
using Servora.Application.Itsm;
using Servora.Application.Platform;
using Servora.Domain.Activity;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Application.Portal;

/// <summary>Default <see cref="IPortalTicketService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="requests">The request service (request details and cancellation, reused from the catalog module).</param>
/// <param name="clock">The clock.</param>
public sealed class PortalTicketService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    INumberSequenceService numbers,
    IRequestService requests,
    IClock clock) : IPortalTicketService
{
    private const int MaxPageSize = 50;
    private const int MaxReasonLength = 2000;
    private const string IncidentNotFound = "The issue was not found.";
    private const string RequestNotFound = "The request was not found.";

    /// <inheritdoc />
    public async Task<Result<PagedResult<PortalTicketSummary>>> ListAsync(PortalTicketQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PagedResult<PortalTicketSummary>>.Forbidden();
        }

        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var page = Math.Clamp(query.Page, 1, PortalTicketReader.MaxWindow / pageSize);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (items, total) = await PortalTicketReader.ReadAsync(db, me, query.Filter, query.Kind, PortalScope.Term(query.Search), (page - 1) * pageSize, pageSize, cancellationToken);
        return Result<PagedResult<PortalTicketSummary>>.Success(new PagedResult<PortalTicketSummary>(items, total, page, pageSize));
    }

    /// <inheritdoc />
    public async Task<Result<PortalIncidentDetail>> GetIncidentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalIncidentDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await PortalScope.OwnIncidents(db, me).FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident is null)
        {
            return Result<PortalIncidentDetail>.NotFound(IncidentNotFound);
        }

        var users = await ReferenceNameResolver.UsersAsync(db, [incident.CallerId, incident.OpenedById, incident.AssignedToId], cancellationToken);
        var groups = await ReferenceNameResolver.GroupsAsync(db, [incident.AssignmentGroupId], cancellationToken);
        var facts = new List<PortalFact>
        {
            new("Reference", incident.Number),
            new("Category", incident.Category ?? "Not categorized"),
            new("Priority", ItsmLabels.For(incident.Priority)),
            new("Urgency", ItsmLabels.For(incident.Urgency)),
            new("Impact", ItsmLabels.For(incident.Impact)),
            new("Reported via", ItsmLabels.For(incident.Channel)),
        };

        return Result<PortalIncidentDetail>.Success(new PortalIncidentDetail
        {
            Id = incident.Id,
            Number = incident.Number,
            Title = incident.ShortDescription,
            Description = incident.Description,
            Status = PortalStatusMapper.ForIncident(incident.State, incident.OnHoldReason),
            Steps = PortalStatusMapper.IncidentSteps(incident.State, incident.OnHoldReason, incident.OpenedAt, incident.ResolvedAt, incident.ClosedAt),
            Facts = facts,
            CallerName = users.NameOf(incident.CallerId),
            OpenedByName = users.NameOf(incident.OpenedById),
            AssignmentGroupName = groups.NameOf(incident.AssignmentGroupId),
            AssignedToName = users.NameOf(incident.AssignedToId),
            ResolutionNotes = incident.State is IncidentState.Resolved or IncidentState.Closed ? incident.ResolutionNotes : null,
            ResolutionLabel = incident.State is IncidentState.Resolved or IncidentState.Closed && incident.ResolutionCode is { } code ? ItsmLabels.For(code) : null,
            OpenedAt = incident.OpenedAt,
            UpdatedAt = incident.UpdatedAt,
            ResolvedAt = incident.ResolvedAt,
            ClosedAt = incident.ClosedAt,
            CanAcceptResolution = incident.State == IncidentState.Resolved,
            CanReopen = incident.State == IncidentState.Resolved,
            CanComment = incident.Active,
        });
    }

    /// <inheritdoc />
    public async Task<Result<PortalRequestDetail>> GetRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalRequestDetail>.Forbidden();
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            if (!await PortalScope.OwnRequests(db, me).AnyAsync(r => r.Id == id, cancellationToken))
            {
                return Result<PortalRequestDetail>.NotFound(RequestNotFound);
            }
        }

        // The catalog module builds the request view (answers, tasks) and checks ownership again.
        var loaded = await requests.GetAsync(id, cancellationToken);
        if (!loaded.Succeeded || loaded.Value is not { } detail)
        {
            return Result<PortalRequestDetail>.From(loaded);
        }

        var approvalInvolved = detail.Items.Any(i => i.Approval != ApprovalStatus.NotRequested);
        var awaitingApproval = detail.Items.Any(i => i.State == RequestItemState.PendingApproval);
        return Result<PortalRequestDetail>.Success(new PortalRequestDetail
        {
            Id = detail.Id,
            Number = detail.Number,
            Title = detail.ShortDescription,
            Status = PortalStatusMapper.ForRequest(detail.State, awaitingApproval),
            Steps = PortalStatusMapper.RequestSteps(detail.State, approvalInvolved, detail.OpenedAt, detail.ClosedAt),
            Items = detail.Items.Select(i => new PortalRequestItemView
            {
                Id = i.Id,
                Number = i.Number,
                Name = i.ItemName,
                Icon = i.Icon,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                IsMonthly = i.BillingType == Servora.Domain.Catalog.CatalogBillingType.Monthly,
                Status = PortalStatusMapper.ForRequestItem(i.State),
                ApprovalLabel = PortalStatusMapper.ApprovalLabel(i.Approval),
                Answers = i.Answers,
                TaskCount = i.Tasks.Count,
                TasksDone = i.Tasks.Count(t => t.State is CatalogTaskState.Completed or CatalogTaskState.Cancelled),
            }).ToList(),
            RequestedForName = detail.RequestedForName,
            OpenedByName = detail.OpenedByName,
            OpenedAt = detail.OpenedAt,
            DueAt = detail.DueAt,
            ClosedAt = detail.ClosedAt,
            TotalPrice = detail.TotalPrice,
            CanCancel = detail.CanCancel,
            CanComment = PortalScope.IsOpen(detail.State),
        });
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> GetIssueCategoriesAsync(CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is null)
        {
            return Result<IReadOnlyList<string>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        IReadOnlyList<string> names = await db.Set<IncidentCategory>().AsNoTracking()
            .Where(c => c.ParentId == null && c.IsActive)
            .OrderBy(c => c.Order).ThenBy(c => c.Name)
            .Select(c => c.Name)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<string>>.Success(names);
    }

    /// <inheritdoc />
    public async Task<Result<PortalTicketSummary>> ReportIssueAsync(PortalIssueInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalTicketSummary>.Forbidden();
        }

        if (input.Validate() is { } error)
        {
            return Result<PortalTicketSummary>.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var category = ItsmData.Clean(input.Category);
        if (category is not null && !await db.Set<IncidentCategory>().AnyAsync(c => c.ParentId == null && c.IsActive && c.Name == category, cancellationToken))
        {
            return Result<PortalTicketSummary>.Invalid("Select one of the offered categories.");
        }

        var now = clock.UtcNow;
        var incident = new Incident
        {
            Number = await numbers.NextAsync(TableNames.Incident, Incident.NumberPrefix, cancellationToken),
            State = IncidentState.New,
            ShortDescription = input.ShortDescription.Trim(),
            Description = ItsmData.Clean(input.Description),
            Category = category,
            Channel = ContactChannel.Portal,
            Impact = input.Impact,
            Urgency = input.Urgency,
            Priority = PriorityMatrix.Calculate(input.Impact, input.Urgency),
            CallerId = me,
            OpenedById = me,
            OpenedAt = now,
        };
        db.Set<Incident>().Add(incident);
        db.Set<ActivityEntry>().Add(ItsmActivity.System(TableNames.Incident, incident.Id, $"Incident {incident.Number} reported by {currentUser.DisplayName} via the self-service portal.", currentUser, now));
        var saved = await ItsmData.SaveAsync(db, "issue", cancellationToken);
        if (!saved.Succeeded)
        {
            return Result<PortalTicketSummary>.From(saved);
        }

        return Result<PortalTicketSummary>.Success(new PortalTicketSummary
        {
            Kind = PortalTicketKind.Incident,
            Id = incident.Id,
            Number = incident.Number,
            Title = incident.ShortDescription,
            Status = PortalStatusMapper.ForIncident(incident.State),
            Icon = "alert-circle",
            OpenedAt = now,
            UpdatedAt = now,
        });
    }

    /// <inheritdoc />
    public Task<Result> AcceptResolutionAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateIncidentAsync(id, (db, incident, me, now) =>
        {
            if (incident.State != IncidentState.Resolved)
            {
                return "Only resolved issues can be confirmed.";
            }

            incident.State = IncidentState.Closed;
            incident.ClosedAt = now;
            incident.ClosedById = me;
            db.Set<ActivityEntry>().Add(ItsmActivity.System(TableNames.Incident, incident.Id, $"{currentUser.DisplayName} confirmed the resolution in the portal – incident closed.", currentUser, now));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ReopenIncidentAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        MutateIncidentAsync(id, (db, incident, me, now) =>
        {
            var text = ItsmData.Clean(reason);
            if (text is null)
            {
                return "Please tell us why the problem is not solved.";
            }

            if (text.Length > MaxReasonLength)
            {
                return $"The explanation must not exceed {MaxReasonLength} characters.";
            }

            if (incident.State != IncidentState.Resolved)
            {
                return "Only resolved issues can be reopened.";
            }

            incident.State = IncidentState.InProgress;
            incident.ReopenCount++;
            incident.ResolutionCode = null;
            incident.ResolutionNotes = null;
            db.Set<ActivityEntry>().Add(new ActivityEntry
            {
                TableName = TableNames.Incident,
                RecordId = incident.Id,
                Type = ActivityType.Comment,
                Text = text,
                AuthorId = me,
                AuthorName = currentUser.DisplayName,
                CreatedAt = now,
            });
            db.Set<ActivityEntry>().Add(ItsmActivity.System(TableNames.Incident, incident.Id, $"Incident reopened by the caller in the portal (#{incident.ReopenCount}).", currentUser, now.AddMilliseconds(1)));
            return null;
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> CancelRequestAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result.Forbidden();
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            if (!await PortalScope.OwnRequests(db, me).AnyAsync(r => r.Id == id, cancellationToken))
            {
                return Result.NotFound(RequestNotFound);
            }
        }

        // The catalog module withdraws approvals, cancels tasks and rolls the state up (and re-checks ownership).
        return await requests.CancelRequestAsync(id, reason, cancellationToken);
    }

    private async Task<Result> MutateIncidentAsync(Guid id, Func<IAppDbContext, Incident, Guid, DateTimeOffset, string?> mutate, CancellationToken cancellationToken)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.Set<Incident>().FirstOrDefaultAsync(i => i.Id == id && (i.CallerId == me || i.OpenedById == me), cancellationToken);
        if (incident is null)
        {
            return Result.NotFound(IncidentNotFound);
        }

        if (mutate(db, incident, me, clock.UtcNow) is { } error)
        {
            return Result.Invalid(error);
        }

        return await ItsmData.SaveAsync(db, "issue", cancellationToken);
    }
}
