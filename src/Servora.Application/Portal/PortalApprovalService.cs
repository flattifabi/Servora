using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Approvals;
using Servora.Application.Catalog;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Changes;
using Servora.Domain.Common;

namespace Servora.Application.Portal;

/// <summary>Default <see cref="IPortalApprovalService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
public sealed class PortalApprovalService(IAppDbContextFactory dbFactory, ICurrentUser currentUser) : IPortalApprovalService
{
    private const string NotFound = "The approval was not found.";

    /// <inheritdoc />
    public async Task<Result<PortalApprovalSubject>> GetSubjectAsync(Guid approvalId, CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalApprovalSubject>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var approval = await db.Set<Approval>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
        if (approval is null || (approval.ApproverId != me && !await currentUser.HasPermissionAsync(Permissions.Approvals.Read, cancellationToken)))
        {
            // Never reveal approvals of other approvers.
            return Result<PortalApprovalSubject>.NotFound(NotFound);
        }

        var subject = approval.TableName switch
        {
            TableNames.RequestItem => await DescribeRequestItemAsync(db, approval, cancellationToken),
            TableNames.ChangeRequest => await DescribeChangeAsync(db, approval, cancellationToken),
            _ => null,
        };

        return Result<PortalApprovalSubject>.Success(subject ?? new PortalApprovalSubject
        {
            ApprovalId = approval.Id,
            Title = approval.RecordSummary ?? approval.RecordNumber ?? "Approval",
            Description = approval.Reason,
            Facts = [new PortalFact("Reference", approval.RecordNumber)],
        });
    }

    private static async Task<PortalApprovalSubject?> DescribeRequestItemAsync(IAppDbContext db, Approval approval, CancellationToken cancellationToken)
    {
        var item = await db.Set<RequestItem>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == approval.RecordId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var catalogItem = await db.Set<CatalogItem>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == item.CatalogItemId, cancellationToken);
        var definitions = await db.Set<CatalogVariable>().AsNoTracking()
            .Where(v => v.CatalogItemId == item.CatalogItemId)
            .OrderBy(v => v.Order).ThenBy(v => v.Name)
            .Take(CatalogVariableValidator.MaxVariablesPerItem * 2)
            .ToListAsync(cancellationToken);
        var answers = PortalAnswerFormatter.Parse(item.VariablesJson);
        var referencedUsers = definitions.Where(d => d.Type == CatalogVariableType.UserReference)
            .Select(d => Guid.TryParse(answers.GetValueOrDefault(d.Name), out var id) ? id : (Guid?)null);
        var users = await ReferenceNameResolver.UsersAsync(db, [item.RequestedForId, item.OpenedById, .. referencedUsers], cancellationToken);
        var total = item.UnitPrice * item.Quantity;

        return new PortalApprovalSubject
        {
            ApprovalId = approval.Id,
            Title = item.ShortDescription,
            Icon = catalogItem?.Icon ?? "package",
            Description = catalogItem?.ShortDescription,
            Facts =
            [
                new PortalFact("Reference", item.Number),
                new PortalFact("Requested for", users.NameOf(item.RequestedForId)),
                new PortalFact("Ordered by", users.NameOf(item.OpenedById)),
                new PortalFact("Quantity", item.Quantity.ToString(CultureInfo.InvariantCulture)),
                new PortalFact("Total price", total == 0 ? "Free" : total.ToString("#,##0.00", CultureInfo.InvariantCulture)),
                new PortalFact("Delivery", catalogItem is null ? null : catalogItem.DeliveryDays == 0 ? "Same day" : $"{catalogItem.DeliveryDays} day(s)"),
            ],
            Answers = PortalAnswerFormatter.Build(definitions, answers, users),
        };
    }

    private static async Task<PortalApprovalSubject?> DescribeChangeAsync(IAppDbContext db, Approval approval, CancellationToken cancellationToken)
    {
        var change = await db.Set<ChangeRequest>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == approval.RecordId, cancellationToken);
        if (change is null)
        {
            return null;
        }

        var users = await ReferenceNameResolver.UsersAsync(db, [change.RequestedById], cancellationToken);
        var cis = await ReferenceNameResolver.ConfigurationItemsAsync(db, [change.ConfigurationItemId], cancellationToken);
        return new PortalApprovalSubject
        {
            ApprovalId = approval.Id,
            Title = change.ShortDescription,
            Icon = "git-pull-request",
            Description = change.Justification ?? change.Description,
            Facts =
            [
                new PortalFact("Reference", change.Number),
                new PortalFact("Type", change.Type.ToString()),
                new PortalFact("Risk", change.Risk == ChangeRisk.VeryHigh ? "Very high" : change.Risk.ToString()),
                new PortalFact("Requested by", users.NameOf(change.RequestedById)),
                new PortalFact("Affected CI", cis.NameOf(change.ConfigurationItemId)),
                new PortalFact("Planned start", Format(change.PlannedStartAt)),
                new PortalFact("Planned end", Format(change.PlannedEndAt)),
            ],
        };
    }

    private static string? Format(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("d MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
