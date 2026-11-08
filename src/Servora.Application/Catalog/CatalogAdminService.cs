using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Approvals;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>Default <see cref="ICatalogAdminService"/> implementation. Configuration changes are audited.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
/// <param name="audit">The audit service.</param>
/// <param name="images">The catalog image store.</param>
public sealed partial class CatalogAdminService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IClock clock, IAuditService audit, ICatalogImageStore images) : ICatalogAdminService
{
    /// <summary>The maximum number of order form sections of one item.</summary>
    public const int MaxSectionsPerItem = 12;

    private const int MaxCategories = 200;
    private const int MaxFulfillmentTasks = 10;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CatalogCategoryModel>>> ListCategoriesAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<CatalogCategoryModel>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        IReadOnlyList<CatalogCategoryModel> categories = await db.Set<CatalogCategory>().AsNoTracking()
            .OrderBy(c => c.Order).ThenBy(c => c.Name)
            .Take(MaxCategories)
            .Select(c => new CatalogCategoryModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Icon = c.Icon,
                Order = c.Order,
                IsActive = c.IsActive,
                ItemCount = db.Set<CatalogItem>().Count(i => i.CategoryId == c.Id),
            })
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CatalogCategoryModel>>.Success(categories);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveCategoryAsync(CatalogCategoryModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var name = model.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 100)
        {
            return Result<Guid>.Invalid("The category name is required (max. 100 characters).");
        }

        if (model.Description is { Length: > 1000 })
        {
            return Result<Guid>.Invalid("The description must not exceed 1000 characters.");
        }

        if (!IconPattern().IsMatch(model.Icon ?? string.Empty))
        {
            return Result<Guid>.Invalid("The icon name is invalid.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Set<CatalogCategory>().AnyAsync(c => c.Name == name && c.Id != model.Id, cancellationToken))
        {
            return Result<Guid>.Conflict($"A category named \"{name}\" already exists.");
        }

        CatalogCategory? category;
        if (model.Id is { } id)
        {
            category = await db.Set<CatalogCategory>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (category is null)
            {
                return Result<Guid>.NotFound("The category was not found.");
            }
        }
        else
        {
            category = new CatalogCategory();
            db.Set<CatalogCategory>().Add(category);
        }

        category.Name = name;
        category.Description = Clean(model.Description);
        category.Icon = model.Icon!;
        category.Order = Math.Clamp(model.Order, 0, 100_000);
        category.IsActive = model.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(model.Id is null ? "category.created" : "category.updated", CatalogTables.Category, category.Id, category.Name, cancellationToken);
        return Result<Guid>.Success(category.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var category = await db.Set<CatalogCategory>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.NotFound("The category was not found.");
        }

        if (await db.Set<CatalogItem>().AnyAsync(i => i.CategoryId == id, cancellationToken))
        {
            return Result.Conflict("The category still contains items. Move or delete them first.");
        }

        db.Set<CatalogCategory>().Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("category.deleted", CatalogTables.Category, id, category.Name, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<CatalogItemSummary>>> ListItemsAsync(Guid? categoryId, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<PagedResult<CatalogItemSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = db.Set<CatalogItem>().AsNoTracking();
        if (categoryId is { } category)
        {
            items = items.Where(i => i.CategoryId == category);
        }

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            items = items.Where(i => i.Name.ToLower().Contains(term) || (i.ShortDescription != null && i.ShortDescription.ToLower().Contains(term)));
        }

        var query =
            from item in items
            join cat in db.Set<CatalogCategory>().AsNoTracking() on item.CategoryId equals cat.Id into categories
            from cat in categories.DefaultIfEmpty()
            select new
            {
                Item = item,
                CategoryName = cat != null ? cat.Name : null,
                VariableCount = db.Set<CatalogVariable>().Count(v => v.CatalogItemId == item.Id),
            };

        query = (page.SortBy, page.SortDescending) switch
        {
            ("Category", false) => query.OrderBy(r => r.CategoryName).ThenBy(r => r.Item.Name),
            ("Category", true) => query.OrderByDescending(r => r.CategoryName).ThenBy(r => r.Item.Name),
            ("Updated", false) => query.OrderBy(r => r.Item.UpdatedAt),
            ("Updated", true) => query.OrderByDescending(r => r.Item.UpdatedAt),
            ("Name", true) => query.OrderByDescending(r => r.Item.Name),
            _ => query.OrderBy(r => r.Item.Name),
        };

        var paged = await query.ToPagedResultAsync(page, cancellationToken);
        var groups = await ReferenceNameResolver.GroupsAsync(db, paged.Items.Select(r => r.Item.FulfillmentGroupId), cancellationToken);
        var rows = paged.Items.Select(r => new CatalogItemSummary
        {
            Id = r.Item.Id,
            Name = r.Item.Name,
            ShortDescription = r.Item.ShortDescription,
            Icon = r.Item.Icon,
            CategoryName = r.CategoryName,
            Price = r.Item.Price,
            DeliveryDays = r.Item.DeliveryDays,
            ApprovalRequired = r.Item.ApprovalRequired,
            FulfillmentGroupName = groups.NameOf(r.Item.FulfillmentGroupId),
            VariableCount = r.VariableCount,
            IsActive = r.Item.IsActive,
            UpdatedAt = r.Item.UpdatedAt,
        }).ToList();
        return Result<PagedResult<CatalogItemSummary>>.Success(new PagedResult<CatalogItemSummary>(rows, paged.TotalCount, paged.Page, paged.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<CatalogAdminStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<CatalogAdminStats>.Forbidden();
        }

        var since = clock.UtcNow.AddDays(-30);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = db.Set<CatalogItem>().AsNoTracking();
        return Result<CatalogAdminStats>.Success(new CatalogAdminStats(
            await items.CountAsync(i => i.IsActive, cancellationToken),
            await items.CountAsync(i => !i.IsActive, cancellationToken),
            await db.Set<CatalogCategory>().CountAsync(cancellationToken),
            await items.CountAsync(i => i.IsActive && i.ApprovalRequired, cancellationToken),
            await db.Set<RequestItem>().CountAsync(r => r.OpenedAt >= since, cancellationToken)));
    }

    /// <inheritdoc />
    public async Task<Result<CatalogItemModel>> GetItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<CatalogItemModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Set<CatalogItem>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Result<CatalogItemModel>.NotFound("The catalog item was not found.");
        }

        var variables = await LoadVariablesAsync(db, id, cancellationToken);
        var sections = await LoadSectionsAsync(db, id, cancellationToken);
        return Result<CatalogItemModel>.Success(new CatalogItemModel
        {
            Id = item.Id,
            Name = item.Name,
            ShortDescription = item.ShortDescription,
            Description = item.Description,
            Icon = item.Icon,
            CategoryId = item.CategoryId,
            Price = item.Price,
            DeliveryDays = item.DeliveryDays,
            FulfillmentGroupId = item.FulfillmentGroupId,
            FulfillmentTasks = item.FulfillmentTasks,
            ApprovalRequired = item.ApprovalRequired,
            ApprovalGroupId = item.ApprovalGroupId,
            ManagerApproval = item.ManagerApproval,
            ApprovalMode = item.ApprovalMode,
            Order = item.Order,
            IsActive = item.IsActive,
            Version = item.Version,
            Variables = variables.Select(CatalogVariableDefinition.From).ToList(),
            Sections = sections.Select(s => new CatalogSectionModel { Id = s.Id, Title = s.Title, Description = s.Description, Order = s.Order }).ToList(),
            BillingType = item.BillingType,
            TermMonths = item.TermMonths,
            OneTimePrice = item.OneTimePrice,
            Subtitle = item.Subtitle,
            BadgeText = item.BadgeText,
            Code = item.Code,
            ImageId = item.ImageId,
            Teaser = item.Teaser,
            TeaserText = item.TeaserText,
            DeliveryText = item.DeliveryText,
            Highlights = item.Highlights,
            DetailsMarkdown = item.DetailsMarkdown,
            DeliveryMarkdown = item.DeliveryMarkdown,
            IsFeatured = item.IsFeatured,
            FeaturedOrder = item.FeaturedOrder,
        });
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateItemAsync(CatalogItemModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (ValidateItem(model) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        var item = new CatalogItem();
        Apply(item, model);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<CatalogItem>().Add(item);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("item.created", CatalogTables.Item, item.Id, item.Name, cancellationToken);
        return Result<Guid>.Success(item.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateItemAsync(Guid id, CatalogItemModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        if (ValidateItem(model) is { } error)
        {
            return Result.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Set<CatalogItem>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("The catalog item was not found.");
        }

        if (model.Version != 0 && item.Version != model.Version)
        {
            return Result.Conflict("The item was modified by someone else. Reload it and apply your changes again.");
        }

        Apply(item, model);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The item was modified by someone else. Reload it and apply your changes again.");
        }

        await AuditAsync("item.updated", CatalogTables.Item, item.Id, item.Name, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> DuplicateItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Set<CatalogItem>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (source is null)
        {
            return Result<Guid>.NotFound("The catalog item was not found.");
        }

        var copy = new CatalogItem
        {
            Name = Truncate($"{source.Name} (copy)", 200),
            ShortDescription = source.ShortDescription,
            Description = source.Description,
            Icon = source.Icon,
            CategoryId = source.CategoryId,
            Price = source.Price,
            DeliveryDays = source.DeliveryDays,
            FulfillmentGroupId = source.FulfillmentGroupId,
            FulfillmentTasks = source.FulfillmentTasks,
            ApprovalRequired = source.ApprovalRequired,
            ApprovalGroupId = source.ApprovalGroupId,
            ManagerApproval = source.ManagerApproval,
            ApprovalMode = source.ApprovalMode,
            Order = source.Order,
            IsActive = false,
            BillingType = source.BillingType,
            TermMonths = source.TermMonths,
            OneTimePrice = source.OneTimePrice,
            Subtitle = source.Subtitle,
            BadgeText = source.BadgeText,
            Code = null,
            Teaser = source.Teaser,
            TeaserText = source.TeaserText,
            DeliveryText = source.DeliveryText,
            Highlights = source.Highlights,
            DetailsMarkdown = source.DetailsMarkdown,
            DeliveryMarkdown = source.DeliveryMarkdown,
            IsFeatured = false,
            FeaturedOrder = source.FeaturedOrder,
        };
        if (source.ImageId is { } sourceImage && images.Open(sourceImage) is { } imageStream)
        {
            // The copy gets its own file so that deleting one item never removes the other item's image.
            await using (imageStream)
            {
                using var buffer = new MemoryStream();
                await imageStream.CopyToAsync(buffer, cancellationToken);
                copy.ImageId = await images.SaveAsync(buffer.ToArray(), cancellationToken);
            }
        }

        db.Set<CatalogItem>().Add(copy);
        var sectionMap = new Dictionary<Guid, Guid>();
        foreach (var section in await LoadSectionsAsync(db, id, cancellationToken))
        {
            var sectionCopy = new CatalogSection { CatalogItemId = copy.Id, Title = section.Title, Description = section.Description, Order = section.Order };
            sectionMap[section.Id] = sectionCopy.Id;
            db.Set<CatalogSection>().Add(sectionCopy);
        }

        foreach (var variable in await LoadVariablesAsync(db, id, cancellationToken))
        {
            db.Set<CatalogVariable>().Add(new CatalogVariable
            {
                CatalogItemId = copy.Id,
                SectionId = variable.SectionId is { } sectionId && sectionMap.TryGetValue(sectionId, out var mapped) ? mapped : null,
                PriceDelta = variable.PriceDelta,
                Name = variable.Name,
                Label = variable.Label,
                Type = variable.Type,
                Mandatory = variable.Mandatory,
                Order = variable.Order,
                HelpText = variable.HelpText,
                Options = variable.Options,
                DefaultValue = variable.DefaultValue,
                MaxLength = variable.MaxLength,
                MinValue = variable.MinValue,
                MaxValue = variable.MaxValue,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("item.duplicated", CatalogTables.Item, copy.Id, copy.Name, cancellationToken);
        return Result<Guid>.Success(copy.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Set<CatalogItem>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("The catalog item was not found.");
        }

        if (await db.Set<RequestItem>().AnyAsync(r => r.CatalogItemId == id, cancellationToken))
        {
            return Result.Conflict("The item was already ordered and cannot be deleted. Deactivate it instead.");
        }

        var variables = await LoadVariablesAsync(db, id, cancellationToken, tracked: true);
        db.Set<CatalogVariable>().RemoveRange(variables);
        db.Set<CatalogSection>().RemoveRange(await db.Set<CatalogSection>().Where(s => s.CatalogItemId == id).Take(MaxSectionsPerItem * 4).ToListAsync(cancellationToken));
        db.Set<CatalogItem>().Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        if (item.ImageId is { } imageId)
        {
            images.Delete(imageId);
        }

        await AuditAsync("item.deleted", CatalogTables.Item, id, item.Name, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveVariableAsync(Guid itemId, CatalogVariableModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Set<CatalogItem>().AnyAsync(i => i.Id == itemId, cancellationToken))
        {
            return Result<Guid>.NotFound("The catalog item was not found.");
        }

        var variables = await LoadVariablesAsync(db, itemId, cancellationToken, tracked: true);
        var others = variables.Where(v => v.Id != model.Id).Select(v => v.Name).ToList();
        if (CatalogVariableValidator.ValidateDefinition(model, others) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        if (model.SectionId is { } sectionId && !await db.Set<CatalogSection>().AnyAsync(s => s.Id == sectionId && s.CatalogItemId == itemId, cancellationToken))
        {
            return Result<Guid>.Invalid("The selected section does not belong to this item.");
        }

        CatalogVariable? variable;
        if (model.Id is { } id && id != Guid.Empty)
        {
            variable = variables.FirstOrDefault(v => v.Id == id);
            if (variable is null)
            {
                return Result<Guid>.NotFound("The variable was not found.");
            }
        }
        else
        {
            if (variables.Count >= CatalogVariableValidator.MaxVariablesPerItem)
            {
                return Result<Guid>.Invalid($"An item can have at most {CatalogVariableValidator.MaxVariablesPerItem} variables.");
            }

            variable = new CatalogVariable { CatalogItemId = itemId, Order = (variables.Count == 0 ? 0 : variables.Max(v => v.Order)) + 10 };
            db.Set<CatalogVariable>().Add(variable);
        }

        var definition = model.ToDefinition();
        variable.Name = definition.Name;
        variable.Label = definition.Label;
        variable.Type = definition.Type;
        variable.Mandatory = definition.Mandatory;
        variable.HelpText = definition.HelpText;
        variable.Options = definition.Type == CatalogVariableType.Choice ? string.Join('\n', definition.Choices.Select(c => c.ToLine())) : null;
        variable.SectionId = definition.SectionId;
        variable.PriceDelta = definition.PriceDelta;
        variable.DefaultValue = definition.DefaultValue;
        variable.MaxLength = definition.Type is CatalogVariableType.Text or CatalogVariableType.MultilineText ? definition.MaxLength : null;
        variable.MinValue = definition.Type == CatalogVariableType.Number ? definition.MinValue : null;
        variable.MaxValue = definition.Type == CatalogVariableType.Number ? definition.MaxValue : null;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(model.Id is null ? "variable.created" : "variable.updated", CatalogTables.Variable, variable.Id, variable.Name, cancellationToken);
        return Result<Guid>.Success(variable.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteVariableAsync(Guid variableId, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var variable = await db.Set<CatalogVariable>().FirstOrDefaultAsync(v => v.Id == variableId, cancellationToken);
        if (variable is null)
        {
            return Result.NotFound("The variable was not found.");
        }

        db.Set<CatalogVariable>().Remove(variable);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("variable.deleted", CatalogTables.Variable, variableId, variable.Name, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveSectionAsync(Guid itemId, CatalogSectionModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var title = model.Title?.Trim() ?? string.Empty;
        if (title.Length is 0 or > 120)
        {
            return Result<Guid>.Invalid("The section title is required (max. 120 characters).");
        }

        if (model.Description is { Length: > 300 })
        {
            return Result<Guid>.Invalid("The section description must not exceed 300 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Set<CatalogItem>().AnyAsync(i => i.Id == itemId, cancellationToken))
        {
            return Result<Guid>.NotFound("The catalog item was not found.");
        }

        CatalogSection? section;
        if (model.Id is { } id && id != Guid.Empty)
        {
            section = await db.Set<CatalogSection>().FirstOrDefaultAsync(s => s.Id == id && s.CatalogItemId == itemId, cancellationToken);
            if (section is null)
            {
                return Result<Guid>.NotFound("The section was not found.");
            }
        }
        else
        {
            var count = await db.Set<CatalogSection>().CountAsync(s => s.CatalogItemId == itemId, cancellationToken);
            if (count >= MaxSectionsPerItem)
            {
                return Result<Guid>.Invalid($"An item can have at most {MaxSectionsPerItem} sections.");
            }

            section = new CatalogSection { CatalogItemId = itemId };
            db.Set<CatalogSection>().Add(section);
        }

        section.Title = title;
        section.Description = Clean(model.Description);
        section.Order = Math.Clamp(model.Order, 0, 100_000);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(model.Id is null ? "section.created" : "section.updated", CatalogTables.Section, section.Id, section.Title, cancellationToken);
        return Result<Guid>.Success(section.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteSectionAsync(Guid sectionId, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var section = await db.Set<CatalogSection>().FirstOrDefaultAsync(s => s.Id == sectionId, cancellationToken);
        if (section is null)
        {
            return Result.NotFound("The section was not found.");
        }

        var members = await db.Set<CatalogVariable>().Where(v => v.SectionId == sectionId).Take(CatalogVariableValidator.MaxVariablesPerItem * 2).ToListAsync(cancellationToken);
        foreach (var variable in members)
        {
            variable.SectionId = null;
        }

        db.Set<CatalogSection>().Remove(section);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("section.deleted", CatalogTables.Section, sectionId, section.Title, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UploadItemImageAsync(Guid itemId, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > ICatalogImageStore.MaxImageBytes)
            {
                return Result.Invalid($"The image must not exceed {ICatalogImageStore.MaxImageBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        var bytes = buffer.ToArray();
        var contentType = LogoImageValidator.DetectContentType(bytes.AsSpan(0, Math.Min(bytes.Length, LogoImageValidator.HeaderLength)));
        if (contentType is null)
        {
            return Result.Invalid("Upload a PNG, JPEG, GIF or WebP image. SVG files are not accepted.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Set<CatalogItem>().FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("The catalog item was not found.");
        }

        var previous = item.ImageId;
        item.ImageId = await images.SaveAsync(bytes, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (previous is { } old)
        {
            images.Delete(old);
        }

        await audit.WriteAsync(new AuditEntry("catalog", "item.image.updated")
        {
            TargetType = CatalogTables.Item,
            TargetId = item.Id.ToString(),
            TargetName = item.Name,
            Message = $"Product image replaced ({contentType}, {bytes.Length / 1024.0:0.#} KB).",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RemoveItemImageAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Set<CatalogItem>().FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("The catalog item was not found.");
        }

        if (item.ImageId is not { } imageId)
        {
            return Result.Success();
        }

        item.ImageId = null;
        await db.SaveChangesAsync(cancellationToken);
        images.Delete(imageId);
        await AuditAsync("item.image.removed", CatalogTables.Item, item.Id, item.Name, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> MoveVariableAsync(Guid variableId, int direction, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var itemId = await db.Set<CatalogVariable>().Where(v => v.Id == variableId).Select(v => (Guid?)v.CatalogItemId).FirstOrDefaultAsync(cancellationToken);
        if (itemId is null)
        {
            return Result.NotFound("The variable was not found.");
        }

        var variables = await LoadVariablesAsync(db, itemId.Value, cancellationToken, tracked: true);
        var index = variables.FindIndex(v => v.Id == variableId);
        var target = index + Math.Sign(direction);
        if (direction == 0 || target < 0 || target >= variables.Count)
        {
            return Result.Success();
        }

        (variables[index], variables[target]) = (variables[target], variables[index]);
        for (var i = 0; i < variables.Count; i++)
        {
            variables[i].Order = (i + 1) * 10;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Splits the fulfilment task text into task names.</summary>
    /// <param name="text">The newline separated task names.</param>
    /// <returns>The names (at most 10).</returns>
    public static IReadOnlyList<string> ParseFulfillmentTasks(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 0)
                .Take(MaxFulfillmentTasks)
                .Select(t => Truncate(t, 300))
                .ToList();

    private static async Task<List<CatalogVariable>> LoadVariablesAsync(IAppDbContext db, Guid itemId, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = db.Set<CatalogVariable>().Where(v => v.CatalogItemId == itemId);
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.OrderBy(v => v.Order).ThenBy(v => v.Name)
            .Take(CatalogVariableValidator.MaxVariablesPerItem * 2)
            .ToListAsync(cancellationToken);
    }

    private static Task<List<CatalogSection>> LoadSectionsAsync(IAppDbContext db, Guid itemId, CancellationToken cancellationToken) =>
        db.Set<CatalogSection>().AsNoTracking()
            .Where(s => s.CatalogItemId == itemId)
            .OrderBy(s => s.Order).ThenBy(s => s.Title)
            .Take(MaxSectionsPerItem)
            .ToListAsync(cancellationToken);

    private static string? ValidateItem(CatalogItemModel model)
    {
        var name = model.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 200)
        {
            return "The item name is required (max. 200 characters).";
        }

        if (model.ShortDescription is { Length: > 300 } || model.Description is { Length: > 8000 })
        {
            return "The short description (300) or description (8000) is too long.";
        }

        if (!IconPattern().IsMatch(model.Icon ?? string.Empty))
        {
            return "The icon name is invalid.";
        }

        if (model.Price is < 0 or > 1_000_000)
        {
            return "The price must be between 0 and 1,000,000.";
        }

        if (model.DeliveryDays is < 0 or > 365)
        {
            return "The delivery time must be between 0 and 365 days.";
        }

        if (model.FulfillmentTasks is { Length: > 2000 })
        {
            return "The fulfilment task list must not exceed 2000 characters.";
        }

        if (!Enum.IsDefined(model.ApprovalMode))
        {
            return "The approval policy is invalid.";
        }

        if (model.ApprovalRequired && model.ApprovalGroupId is null && !model.ManagerApproval)
        {
            return "Select an approval group or require the manager's approval when approval is required.";
        }

        if (!Enum.IsDefined(model.BillingType))
        {
            return "The billing type is invalid.";
        }

        if (model.TermMonths is < 1 or > 120)
        {
            return "The term must be between 1 and 120 months.";
        }

        if (model.OneTimePrice is < 0 or > 1_000_000)
        {
            return "The one-time costs must be between 0 and 1,000,000.";
        }

        if (model.Subtitle is { Length: > 200 } || model.BadgeText is { Length: > 60 } || model.Teaser is { Length: > 80 }
            || model.TeaserText is { Length: > 160 } || model.DeliveryText is { Length: > 80 } || model.Highlights is { Length: > 1000 })
        {
            return "Subtitle (200), badge (60), teaser (80), teaser text (160), delivery text (80) or highlights (1000) are too long.";
        }

        if (model.DetailsMarkdown is { Length: > 8000 } || model.DeliveryMarkdown is { Length: > 8000 })
        {
            return "The details and delivery texts must not exceed 8000 characters each.";
        }

        if (!string.IsNullOrWhiteSpace(model.Code) && !CodePattern().IsMatch(model.Code.Trim()))
        {
            return "The service ID may only contain letters, digits, dots and dashes (max. 40).";
        }

        return null;
    }

    private static void Apply(CatalogItem item, CatalogItemModel model)
    {
        item.Name = model.Name.Trim();
        item.ShortDescription = Clean(model.ShortDescription);
        item.Description = Clean(model.Description);
        item.Icon = model.Icon;
        item.CategoryId = model.CategoryId;
        item.Price = decimal.Round(model.Price, 2);
        item.DeliveryDays = model.DeliveryDays;
        item.FulfillmentGroupId = model.FulfillmentGroupId;
        var tasks = ParseFulfillmentTasks(model.FulfillmentTasks);
        item.FulfillmentTasks = tasks.Count == 0 ? null : string.Join('\n', tasks);
        item.ApprovalRequired = model.ApprovalRequired;
        item.ApprovalGroupId = model.ApprovalRequired ? model.ApprovalGroupId : null;
        item.ManagerApproval = model.ApprovalRequired && model.ManagerApproval;
        item.ApprovalMode = model.ApprovalRequired ? model.ApprovalMode : ApprovalMode.AnyApprover;
        item.Order = Math.Clamp(model.Order, 0, 100_000);
        item.IsActive = model.IsActive;
        item.BillingType = model.BillingType;
        item.TermMonths = model.BillingType == CatalogBillingType.Monthly ? model.TermMonths : null;
        item.OneTimePrice = model.BillingType == CatalogBillingType.Monthly ? decimal.Round(model.OneTimePrice, 2) : 0m;
        item.Subtitle = Clean(model.Subtitle);
        item.BadgeText = Clean(model.BadgeText);
        item.Code = Clean(model.Code);
        item.Teaser = Clean(model.Teaser);
        item.TeaserText = Clean(model.TeaserText);
        item.DeliveryText = Clean(model.DeliveryText);
        item.Highlights = Clean(model.Highlights);
        item.DetailsMarkdown = Clean(model.DetailsMarkdown);
        item.DeliveryMarkdown = Clean(model.DeliveryMarkdown);
        item.IsFeatured = model.IsFeatured;
        item.FeaturedOrder = Math.Clamp(model.FeaturedOrder, 0, 100_000);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    [GeneratedRegex("^[a-z0-9-]{1,50}$")]
    private static partial Regex IconPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.-]{0,39}$")]
    private static partial Regex CodePattern();

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) =>
        currentUser.HasPermissionAsync(Permissions.Requests.ManageCatalog, cancellationToken);

    private Task AuditAsync(string action, string table, Guid id, string name, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry("catalog", action) { TargetType = table, TargetId = id.ToString(), TargetName = name }, cancellationToken);
}
