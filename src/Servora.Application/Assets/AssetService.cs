using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Domain.Assets;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;

namespace Servora.Application.Assets;

/// <summary>Default <see cref="IAssetService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="directory">The directory look-ups.</param>
/// <param name="activity">The activity stream.</param>
/// <param name="audit">The audit log.</param>
/// <param name="clock">The clock.</param>
public sealed class AssetService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    INumberSequenceService numbers,
    IDirectoryLookupService directory,
    IActivityService activity,
    IAuditService audit,
    IClock clock) : IAssetService
{
    private const string NumberPrefix = "AST";
    private const int ExpiringDays = 90;
    private const int MaxOverviewRows = 50_000;

    /// <inheritdoc />
    public async Task<Result<AssetOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<AssetOverview>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Decimal aggregates are computed in memory (SQLite cannot sum or order decimals); the projection is bounded.
        var rows = await db.Set<Asset>().AsNoTracking()
            .Select(a => new { a.State, a.Category, a.Cost, a.Currency, a.WarrantyEnd, a.Entitlements, a.Installs, a.PurchaseDate })
            .Take(MaxOverviewRows)
            .ToListAsync(cancellationToken);
        var today = Today();
        var soon = today.AddDays(ExpiringDays);
        var active = rows.Where(r => AssetLifecycle.IsActive(r.State)).ToList();
        var licenses = active.Where(r => r.Category == AssetCategory.License).ToList();
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var months = Enumerable.Range(0, 12).Select(i => firstMonth.AddMonths(i)).ToList();

        var overview = new AssetOverview
        {
            Total = rows.Count,
            ByState = rows.GroupBy(r => r.State).ToDictionary(g => g.Key, g => g.Count()),
            ByCategory = rows.GroupBy(r => r.Category).ToDictionary(g => g.Key, g => g.Count()),
            ActiveValue = active.GroupBy(r => r.Currency).ToDictionary(g => g.Key, g => g.Sum(r => r.Cost)),
            WarrantyExpiringSoon = active.Count(r => r.WarrantyEnd is { } end && end >= today && end <= soon),
            WarrantyExpired = active.Count(r => r.WarrantyEnd is { } end && end < today),
            LicenseEntitlements = licenses.Sum(r => r.Entitlements ?? 0),
            LicenseInstalls = licenses.Sum(r => r.Installs ?? 0),
            OverDeployedLicenses = licenses.Count(r => (r.Installs ?? 0) > (r.Entitlements ?? 0)),
            PurchasesPerMonth = months.Select(m => (m.ToString("MMM", CultureInfo.InvariantCulture),
                rows.Count(r => r.PurchaseDate is { } p && p.Year == m.Year && p.Month == m.Month))).ToList(),
        };

        return Result<AssetOverview>.Success(overview);
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<AssetSummary>>> ListAsync(AssetQuery query, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PagedResult<AssetSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var assets = db.Set<Asset>().AsNoTracking();
        if (query.State is { } state)
        {
            assets = assets.Where(a => a.State == state);
        }

        if (query.Category is { } category)
        {
            assets = assets.Where(a => a.Category == category);
        }

        if (query.ActiveOnly || query.WarrantyExpiringOnly)
        {
            assets = assets.Where(a => a.State == AssetState.InStock || a.State == AssetState.InUse || a.State == AssetState.InRepair);
        }

        if (query.WarrantyExpiringOnly)
        {
            var soon = Today().AddDays(ExpiringDays);
            assets = assets.Where(a => a.WarrantyEnd != null && a.WarrantyEnd <= soon);
        }

        if (query.AssignedToId is { } assignee)
        {
            assets = assets.Where(a => a.AssignedToId == assignee);
        }

        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var term = query.Page.Search.Trim().ToLower();
            assets = assets.Where(a => a.Number.ToLower().Contains(term) || a.DisplayName.ToLower().Contains(term)
                || (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(term)) || (a.AssetTag != null && a.AssetTag.ToLower().Contains(term))
                || (a.Model != null && a.Model.ToLower().Contains(term)));
        }

        var desc = query.Page.SortDescending;
        assets = query.Page.SortBy switch
        {
            "Number" => desc ? assets.OrderByDescending(a => a.Number) : assets.OrderBy(a => a.Number),
            "DisplayName" => desc ? assets.OrderByDescending(a => a.DisplayName) : assets.OrderBy(a => a.DisplayName),
            "Category" => desc ? assets.OrderByDescending(a => a.Category) : assets.OrderBy(a => a.Category),
            "State" => desc ? assets.OrderByDescending(a => a.State) : assets.OrderBy(a => a.State),
            "WarrantyEnd" => desc ? assets.OrderByDescending(a => a.WarrantyEnd) : assets.OrderBy(a => a.WarrantyEnd),
            "Location" => desc ? assets.OrderByDescending(a => a.Location) : assets.OrderBy(a => a.Location),
            _ => assets.OrderByDescending(a => a.Number),
        };

        var page = await assets.ToPagedResultAsync(query.Page, cancellationToken);
        var users = await directory.GetUsersAsync(page.Items.Where(a => a.AssignedToId is not null).Select(a => a.AssignedToId!.Value), cancellationToken);
        var rows = page.Items.Select(a => new AssetSummary(a.Id, a.Number, a.DisplayName, a.Category, a.Model, a.Manufacturer, a.SerialNumber, a.AssetTag, a.State,
            a.AssignedToId is { } u ? users.GetValueOrDefault(u)?.Display : null, a.Location, a.Cost, a.Currency, a.WarrantyEnd, a.Entitlements, a.Installs)).ToList();
        return Result<PagedResult<AssetSummary>>.Success(new PagedResult<AssetSummary>(rows, page.TotalCount, page.Page, page.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<AssetDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<AssetDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var asset = await db.Set<Asset>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return Result<AssetDetail>.NotFound("The asset was not found.");
        }

        var user = asset.AssignedToId is { } userId ? (await directory.GetUsersAsync([userId], cancellationToken)).GetValueOrDefault(userId) : null;
        var ci = asset.ConfigurationItemId is { } ciId ? (await directory.GetConfigurationItemsAsync([ciId], cancellationToken)).GetValueOrDefault(ciId) : null;
        return Result<AssetDetail>.Success(new AssetDetail
        {
            Id = asset.Id,
            Number = asset.Number,
            State = asset.State,
            Values = new AssetInput
            {
                DisplayName = asset.DisplayName,
                Category = asset.Category,
                Model = asset.Model,
                Manufacturer = asset.Manufacturer,
                Vendor = asset.Vendor,
                SerialNumber = asset.SerialNumber,
                AssetTag = asset.AssetTag,
                State = asset.State,
                AssignedToId = asset.AssignedToId,
                Location = asset.Location,
                Cost = asset.Cost,
                Currency = asset.Currency,
                PurchaseDate = asset.PurchaseDate,
                WarrantyEnd = asset.WarrantyEnd,
                ConfigurationItemId = asset.ConfigurationItemId,
                Entitlements = asset.Entitlements,
                Installs = asset.Installs,
                Notes = asset.Notes,
                Version = asset.Version,
            },
            AssignedToName = user?.Display,
            ConfigurationItemName = ci?.Display,
            RetiredAt = asset.RetiredAt,
            CreatedAt = asset.CreatedAt,
            CreatedBy = asset.CreatedBy,
            UpdatedAt = asset.UpdatedAt,
            UpdatedBy = asset.UpdatedBy,
            AllowedTransitions = AssetLifecycle.AllowedTargets(asset.State),
        });
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(AssetInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Assets.Write, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (input.State is not (AssetState.InStock or AssetState.InUse))
        {
            return Result<Guid>.Invalid("New assets start in stock or in use.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var asset = new Asset { State = input.State };
        var error = await ApplyAsync(db, asset, input, cancellationToken);
        if (error is not null)
        {
            return Result<Guid>.Invalid(error);
        }

        asset.Number = await numbers.NextAsync(TableNames.Asset, NumberPrefix, cancellationToken);
        db.Set<Asset>().Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        await activity.AddSystemAsync(TableNames.Asset, asset.Id, $"Asset registered ({AssetLifecycle.Label(asset.State)}).", cancellationToken);
        return Result<Guid>.Success(asset.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, AssetInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Assets.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var asset = await db.Set<Asset>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return Result.NotFound();
        }

        if (asset.State == AssetState.Disposed)
        {
            return Result.Invalid("Disposed assets are read-only.");
        }

        if (input.Version != 0 && input.Version != asset.Version)
        {
            return Result.Conflict("The asset was changed by someone else. Reload it and apply your changes again.");
        }

        var error = await ApplyAsync(db, asset, input, cancellationToken);
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
            return Result.Conflict("The asset was changed by someone else. Reload it and apply your changes again.");
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ChangeStateAsync(Guid id, AssetState target, string? note = null, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Assets.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var asset = await db.Set<Asset>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return Result.NotFound();
        }

        var from = asset.State;
        if (!AssetLifecycle.CanTransition(from, target))
        {
            return Result.Invalid($"An asset cannot move from {AssetLifecycle.Label(from)} to {AssetLifecycle.Label(target)}.");
        }

        if (target == AssetState.InUse && asset.AssignedToId is null && asset.Category == AssetCategory.Hardware)
        {
            return Result.Invalid("Assign the hardware to a user before putting it in use.");
        }

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note?.Length > 1000)
        {
            return Result.Invalid("The note must not exceed 1000 characters.");
        }

        asset.State = target;
        switch (target)
        {
            case AssetState.Retired:
                asset.RetiredAt = clock.UtcNow;
                break;
            case AssetState.InStock:
                asset.AssignedToId = null;
                asset.RetiredAt = null;
                break;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The asset was changed by someone else. Reload it and try again.");
        }

        await activity.AddSystemAsync(TableNames.Asset, asset.Id, $"Lifecycle: {AssetLifecycle.Label(from)} → {AssetLifecycle.Label(target)}" + (note is null ? "." : $" — {note}"), cancellationToken);
        if (target is AssetState.Retired or AssetState.Disposed)
        {
            await audit.WriteAsync(new AuditEntry("asset", target == AssetState.Retired ? "asset.retire" : "asset.dispose")
            {
                TargetType = TableNames.Asset,
                TargetId = asset.Id.ToString(),
                TargetName = asset.Number,
                Message = note,
            }, cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Assets.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var asset = await db.Set<Asset>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return Result.NotFound();
        }

        db.Set<Asset>().Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("asset", "asset.delete") { TargetType = TableNames.Asset, TargetId = id.ToString(), TargetName = asset.Number, Message = asset.DisplayName }, cancellationToken);
        return Result.Success();
    }

    private static string? Clean(string? value, int max, string label, ref string? error)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            error ??= $"{label} must not exceed {max} characters.";
        }

        return trimmed;
    }

    private async Task<string?> ApplyAsync(IAppDbContext db, Asset asset, AssetInput input, CancellationToken cancellationToken)
    {
        var name = input.DisplayName?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 250)
        {
            return "The display name is required (max 250 characters).";
        }

        if (!Enum.IsDefined(input.Category))
        {
            return "Select a valid category.";
        }

        if (input.Cost is < 0 or > 1_000_000_000m)
        {
            return "The cost must be between 0 and 1,000,000,000.";
        }

        var currency = input.Currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            return "The currency must be a three letter ISO code such as EUR or USD.";
        }

        if (input.PurchaseDate is { } purchased && input.WarrantyEnd is { } warranty && warranty < purchased)
        {
            return "The warranty end must not be before the purchase date.";
        }

        if (input.Entitlements is < 0 || input.Installs is < 0)
        {
            return "Entitlements and installations must not be negative.";
        }

        if (input.ConfigurationItemId is { } ciId && !await db.Set<ConfigurationItem>().AnyAsync(c => c.Id == ciId, cancellationToken))
        {
            return "The linked configuration item does not exist.";
        }

        string? error = null;
        var model = Clean(input.Model, 150, "The model", ref error);
        var manufacturer = Clean(input.Manufacturer, 128, "The manufacturer", ref error);
        var vendor = Clean(input.Vendor, 128, "The vendor", ref error);
        var serial = Clean(input.SerialNumber, 128, "The serial number", ref error);
        var tag = Clean(input.AssetTag, 64, "The asset tag", ref error);
        var location = Clean(input.Location, 128, "The location", ref error);
        var notes = Clean(input.Notes, 4000, "The notes", ref error);
        if (error is not null)
        {
            return error;
        }

        if (tag is not null && await db.Set<Asset>().AnyAsync(a => a.AssetTag == tag && a.Id != asset.Id, cancellationToken))
        {
            return "Another asset already uses this asset tag.";
        }

        asset.DisplayName = name;
        asset.Category = input.Category;
        asset.Model = model;
        asset.Manufacturer = manufacturer;
        asset.Vendor = vendor;
        asset.SerialNumber = serial;
        asset.AssetTag = tag;
        asset.AssignedToId = input.AssignedToId;
        asset.Location = location;
        asset.Cost = decimal.Round(input.Cost, 2);
        asset.Currency = currency;
        asset.PurchaseDate = input.PurchaseDate;
        asset.WarrantyEnd = input.WarrantyEnd;
        asset.ConfigurationItemId = input.ConfigurationItemId;
        asset.Entitlements = input.Category == AssetCategory.License ? input.Entitlements : null;
        asset.Installs = input.Category == AssetCategory.License ? input.Installs : null;
        asset.Notes = notes;
        return null;
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    private Task<bool> CanReadAsync(CancellationToken cancellationToken) => currentUser.HasPermissionAsync(Permissions.Assets.Read, cancellationToken);
}
