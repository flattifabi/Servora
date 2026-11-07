using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Approvals;
using Servora.Application.Common;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;

namespace Servora.Application.Catalog;

/// <summary>Default <see cref="ICatalogOrderService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="activity">The activity stream service.</param>
/// <param name="approvals">The approval engine.</param>
/// <param name="fulfillment">The fulfilment engine.</param>
public sealed class CatalogOrderService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IClock clock,
    INumberSequenceService numbers,
    IActivityService activity,
    ApprovalEngine approvals,
    CatalogFulfillmentEngine fulfillment) : ICatalogOrderService
{
    private const int MaxQuantity = 100;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CatalogCategoryModel>>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanBrowseAsync(cancellationToken))
        {
            return Result<IReadOnlyList<CatalogCategoryModel>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = db.Set<CatalogItem>();
        IReadOnlyList<CatalogCategoryModel> categories = await db.Set<CatalogCategory>().AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Order).ThenBy(c => c.Name)
            .Take(200)
            .Select(c => new CatalogCategoryModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Icon = c.Icon,
                Order = c.Order,
                IsActive = c.IsActive,
                ItemCount = items.Count(i => i.CategoryId == c.Id && i.IsActive),
            })
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CatalogCategoryModel>>.Success(categories.Where(c => c.ItemCount > 0).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CatalogItemCard>>> GetItemsAsync(Guid? categoryId, string? search, int take = 50, CancellationToken cancellationToken = default)
    {
        if (!await CanBrowseAsync(cancellationToken))
        {
            return Result<IReadOnlyList<CatalogItemCard>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<CatalogItem>().AsNoTracking().Where(i => i.IsActive);
        if (categoryId is { } category)
        {
            query = query.Where(i => i.CategoryId == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(term) || (i.ShortDescription != null && i.ShortDescription.ToLower().Contains(term)));
        }

        var rows = await query.OrderBy(i => i.Order).ThenBy(i => i.Name).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        var categoryIds = rows.Where(r => r.CategoryId.HasValue).Select(r => r.CategoryId!.Value).Distinct().ToList();
        var names = await db.Set<CatalogCategory>().AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        IReadOnlyList<CatalogItemCard> cards = rows.Select(r => ToCard(r, r.CategoryId is { } id ? names.GetValueOrDefault(id) : null)).ToList();
        return Result<IReadOnlyList<CatalogItemCard>>.Success(cards);
    }

    /// <inheritdoc />
    public async Task<Result<CatalogOrderForm>> GetOrderFormAsync(Guid catalogItemId, CancellationToken cancellationToken = default)
    {
        if (!await CanBrowseAsync(cancellationToken))
        {
            return Result<CatalogOrderForm>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (item, variables, categoryName) = await LoadItemAsync(db, catalogItemId, cancellationToken);
        if (item is null || !item.IsActive)
        {
            return Result<CatalogOrderForm>.NotFound("The catalog item is not available.");
        }

        return Result<CatalogOrderForm>.Success(new CatalogOrderForm(ToCard(item, categoryName), item.Description, variables));
    }

    /// <inheritdoc />
    public async Task<Result<OrderConfirmation>> OrderAsync(Guid catalogItemId, IReadOnlyDictionary<string, string?> variables, Guid? requestedForId = null, int quantity = 1, CancellationToken cancellationToken = default)
    {
        var canFulfil = await currentUser.HasPermissionAsync(Permissions.Requests.Write, cancellationToken);
        if (!canFulfil && !await currentUser.HasPermissionAsync(Permissions.Portal.Access, cancellationToken))
        {
            return Result<OrderConfirmation>.Forbidden();
        }

        var requestedFor = requestedForId ?? currentUser.UserId;
        if (requestedFor is null)
        {
            return Result<OrderConfirmation>.Invalid("Select the user the item is requested for.");
        }

        if (requestedFor != currentUser.UserId && !canFulfil)
        {
            return Result<OrderConfirmation>.Forbidden("You can only order items for yourself.");
        }

        if (quantity is < 1 or > MaxQuantity)
        {
            return Result<OrderConfirmation>.Invalid($"The quantity must be between 1 and {MaxQuantity}.");
        }

        CatalogItem? item;
        IReadOnlyList<CatalogVariableDefinition> definitions;
        VariableValidationResult validation;
        User? recipient;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            (item, definitions, _) = await LoadItemAsync(db, catalogItemId, cancellationToken);
            if (item is null || !item.IsActive)
            {
                return Result<OrderConfirmation>.NotFound("The catalog item is not available.");
            }

            recipient = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == requestedFor && u.IsActive, cancellationToken);
            if (recipient is null)
            {
                return Result<OrderConfirmation>.Invalid("The requested-for user does not exist or is inactive.");
            }

            validation = CatalogVariableValidator.Validate(definitions, variables);
            if (validation.IsValid)
            {
                validation = await ValidateUserReferencesAsync(db, definitions, validation, cancellationToken);
            }
        }

        if (!validation.IsValid)
        {
            return Result<OrderConfirmation>.Invalid(validation.Summary);
        }

        var approverUsers = new List<Guid>();
        var approverGroups = new List<Guid>();
        if (item.ApprovalRequired)
        {
            if (item.ManagerApproval && recipient.ManagerId is { } managerId)
            {
                approverUsers.Add(managerId);
            }

            if (item.ApprovalGroupId is { } groupId)
            {
                approverGroups.Add(groupId);
            }

            var resolved = await approvals.ResolveApproversAsync(approverUsers, approverGroups, cancellationToken);
            if (resolved.Count == 0)
            {
                return Result<OrderConfirmation>.Failure("This item cannot be ordered right now because no approver is available. Please contact the service desk.");
            }
        }

        var now = clock.UtcNow;
        var due = now.AddDays(item.DeliveryDays);
        var onBehalf = requestedFor != currentUser.UserId;
        var request = new Request
        {
            Number = await numbers.NextAsync(TableNames.Request, CatalogTables.RequestPrefix, cancellationToken),
            ShortDescription = Truncate(onBehalf ? $"{item.Name} for {recipient.DisplayName}" : item.Name, 300),
            RequestedForId = requestedFor,
            OpenedById = currentUser.UserId,
            OpenedAt = now,
            DueAt = due,
            State = RequestState.Submitted,
            TotalPrice = item.Price * quantity,
            Impact = Impact.Low,
            Urgency = Urgency.Low,
            Priority = PriorityMatrix.Calculate(Impact.Low, Urgency.Low),
        };
        var requestItem = new RequestItem
        {
            Number = await numbers.NextAsync(TableNames.RequestItem, CatalogTables.RequestItemPrefix, cancellationToken),
            RequestId = request.Id,
            CatalogItemId = item.Id,
            ShortDescription = Truncate(item.Name, 300),
            Description = item.ShortDescription,
            RequestedForId = requestedFor,
            OpenedById = currentUser.UserId,
            OpenedAt = now,
            DueAt = due,
            Quantity = quantity,
            UnitPrice = item.Price,
            VariablesJson = JsonSerializer.Serialize(validation.Values),
            AssignmentGroupId = item.FulfillmentGroupId,
            State = item.ApprovalRequired ? RequestItemState.PendingApproval : RequestItemState.InFulfillment,
            Approval = item.ApprovalRequired ? ApprovalStatus.Requested : ApprovalStatus.NotRequested,
            Impact = request.Impact,
            Urgency = request.Urgency,
            Priority = request.Priority,
        };

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Set<Request>().Add(request);
            db.Set<RequestItem>().Add(requestItem);
            await db.SaveChangesAsync(cancellationToken);
        }

        await activity.AddSystemAsync(TableNames.Request, request.Id, $"{currentUser.DisplayName} ordered {quantity} × {item.Name}{(onBehalf ? $" for {recipient.DisplayName}" : string.Empty)}.", cancellationToken);

        if (item.ApprovalRequired)
        {
            var reason = $"{item.Name}{(quantity > 1 ? $" ×{quantity}" : string.Empty)} for {recipient.DisplayName}" + (item.Price > 0 ? $" · {request.TotalPrice:0.00}" : string.Empty);
            var policy = item.ApprovalMode == ApprovalMode.AllApprovers ? ApprovalPolicy.AllApprovers : ApprovalPolicy.AnyApprover;
            var requested = await approvals.RequestAsync(new ApprovalRequest(TableNames.RequestItem, requestItem.Id, approverUsers, approverGroups, policy, reason), cancellationToken);
            if (!requested.Succeeded)
            {
                await fulfillment.CancelRequestAsync(request.Id, "Approval could not be requested.", cancellationToken);
                return Result<OrderConfirmation>.From(requested);
            }

            await fulfillment.RecomputeRequestAsync(request.Id, cancellationToken);
        }
        else
        {
            await fulfillment.StartFulfillmentAsync(requestItem.Id, cancellationToken);
        }

        return Result<OrderConfirmation>.Success(new OrderConfirmation(
            request.Id,
            request.Number,
            requestItem.Id,
            requestItem.Number,
            item.ApprovalRequired,
            item.ApprovalRequired ? RequestItemState.PendingApproval : RequestItemState.InFulfillment));
    }

    private static CatalogItemCard ToCard(CatalogItem item, string? categoryName) => new()
    {
        Id = item.Id,
        Name = item.Name,
        ShortDescription = item.ShortDescription,
        Icon = item.Icon,
        CategoryId = item.CategoryId,
        CategoryName = categoryName,
        Price = item.Price,
        DeliveryDays = item.DeliveryDays,
        ApprovalRequired = item.ApprovalRequired,
    };

    private static async Task<(CatalogItem? Item, IReadOnlyList<CatalogVariableDefinition> Variables, string? CategoryName)> LoadItemAsync(IAppDbContext db, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await db.Set<CatalogItem>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null)
        {
            return (null, [], null);
        }

        var variables = await db.Set<CatalogVariable>().AsNoTracking()
            .Where(v => v.CatalogItemId == itemId)
            .OrderBy(v => v.Order).ThenBy(v => v.Name)
            .Take(CatalogVariableValidator.MaxVariablesPerItem * 2)
            .ToListAsync(cancellationToken);
        var categoryName = item.CategoryId is { } categoryId
            ? await db.Set<CatalogCategory>().AsNoTracking().Where(c => c.Id == categoryId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        return (item, variables.Select(CatalogVariableDefinition.From).ToList(), categoryName);
    }

    private static async Task<VariableValidationResult> ValidateUserReferencesAsync(IAppDbContext db, IReadOnlyList<CatalogVariableDefinition> definitions, VariableValidationResult validation, CancellationToken cancellationToken)
    {
        var references = definitions
            .Where(d => d.Type == CatalogVariableType.UserReference && validation.Values.GetValueOrDefault(d.Name) is not null)
            .Select(d => (d, Id: Guid.Parse(validation.Values[d.Name]!)))
            .ToList();
        if (references.Count == 0)
        {
            return validation;
        }

        var ids = references.Select(r => r.Id).Distinct().ToList();
        var existing = await db.Set<User>().AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync(cancellationToken);
        var errors = references.Where(r => !existing.Contains(r.Id)).ToDictionary(r => r.d.Name, r => $"{r.d.Label}: select a valid, active user.");
        return errors.Count == 0 ? validation : new VariableValidationResult(errors, validation.Values);
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    private async Task<bool> CanBrowseAsync(CancellationToken cancellationToken) =>
        await currentUser.HasPermissionAsync(Permissions.Portal.Access, cancellationToken)
        || await currentUser.HasPermissionAsync(Permissions.Requests.Read, cancellationToken)
        || await currentUser.HasPermissionAsync(Permissions.Requests.ManageCatalog, cancellationToken);
}
