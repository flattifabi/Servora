using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>Default <see cref="ICiClassService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit log.</param>
public sealed partial class CiClassService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IAuditService audit) : ICiClassService
{
    private const int MaxClasses = 500;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CiClassDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Read, cancellationToken))
        {
            return Result<IReadOnlyList<CiClassDto>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var classes = await LoadAsync(db, cancellationToken);
        var counts = await db.Set<ConfigurationItem>().GroupBy(c => c.ClassName).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        return Result<IReadOnlyList<CiClassDto>>.Success(classes.Select(c => ToDto(c, counts.GetValueOrDefault(c.Key))).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CiAttributeDefinition>>> GetEffectiveAttributesAsync(string classKey, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Read, cancellationToken))
        {
            return Result<IReadOnlyList<CiAttributeDefinition>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var classes = await LoadAsync(db, cancellationToken);
        return Result<IReadOnlyList<CiAttributeDefinition>>.Success(CiClassHierarchy.EffectiveAttributes(classes, classKey));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(Guid? id, CiClassInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Manage, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var label = input.Label?.Trim() ?? string.Empty;
        if (label.Length is 0 or > 150)
        {
            return Result<Guid>.Invalid("The label is required (max 150 characters).");
        }

        if (!ColorRegex().IsMatch(input.Color ?? string.Empty))
        {
            return Result<Guid>.Invalid("The color must be a hex value such as #7c6cff.");
        }

        if (input.Description?.Length > 1000)
        {
            return Result<Guid>.Invalid("The description must not exceed 1000 characters.");
        }

        var attributes = input.Attributes.Select(a => new CiAttributeDefinition
        {
            Name = a.Name?.Trim() ?? string.Empty,
            Label = a.Label?.Trim() ?? string.Empty,
            Type = a.Type,
            Required = a.Required,
            Choices = a.Choices.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Help = string.IsNullOrWhiteSpace(a.Help) ? null : a.Help.Trim(),
            Unit = string.IsNullOrWhiteSpace(a.Unit) ? null : a.Unit.Trim(),
        }).ToList();
        if (CiAttributeSchema.ValidateDefinitions(attributes) is { } attributeError)
        {
            return Result<Guid>.Invalid(attributeError);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var classes = await db.Set<CiClass>().Take(MaxClasses).ToListAsync(cancellationToken);
        CiClass entity;
        if (id is { } existingId)
        {
            var found = classes.FirstOrDefault(c => c.Id == existingId);
            if (found is null)
            {
                return Result<Guid>.NotFound();
            }

            entity = found;
        }
        else
        {
            var key = input.Key?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!KeyRegex().IsMatch(key))
            {
                return Result<Guid>.Invalid("The key must start with a letter and contain only lower case letters, digits and underscores (2–100 characters).");
            }

            if (classes.Any(c => c.Key == key))
            {
                return Result<Guid>.Conflict("A class with this key already exists.");
            }

            entity = new CiClass { Key = key };
            db.Set<CiClass>().Add(entity);
            classes.Add(entity);
        }

        var parentKey = string.IsNullOrWhiteSpace(input.ParentKey) ? null : input.ParentKey.Trim();
        if (parentKey is not null && classes.All(c => c.Key != parentKey))
        {
            return Result<Guid>.Invalid("The parent class does not exist.");
        }

        if (CiClassHierarchy.WouldCreateCycle(classes, entity.Key, parentKey))
        {
            return Result<Guid>.Invalid("A class cannot inherit from itself or from one of its sub-classes.");
        }

        entity.Label = label;
        entity.Icon = CleanIcon(input.Icon);
        entity.Color = input.Color!.ToLowerInvariant();
        entity.ParentKey = parentKey;
        entity.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        entity.SortOrder = input.SortOrder;
        entity.AttributesJson = CiAttributeSchema.SerializeDefinitions(attributes);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("cmdb", id is null ? "class.create" : "class.update")
        {
            TargetType = CmdbTables.CiClass,
            TargetId = entity.Id.ToString(),
            TargetName = entity.Key,
            Details = new { attributes = attributes.Select(a => a.Name).ToArray(), parent = parentKey },
        }, cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Cmdb.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<CiClass>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound();
        }

        if (entity.IsSystem)
        {
            return Result.Invalid("Built-in classes cannot be deleted.");
        }

        if (await db.Set<CiClass>().AnyAsync(c => c.ParentKey == entity.Key, cancellationToken))
        {
            return Result.Conflict("The class has sub-classes. Delete or re-parent them first.");
        }

        if (await db.Set<ConfigurationItem>().AnyAsync(c => c.ClassName == entity.Key, cancellationToken))
        {
            return Result.Conflict("Configuration items of this class exist. Reclassify or delete them first.");
        }

        db.Set<CiClass>().Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("cmdb", "class.delete") { TargetType = CmdbTables.CiClass, TargetId = id.ToString(), TargetName = entity.Key }, cancellationToken);
        return Result.Success();
    }

    /// <summary>Loads all classes ordered for display.</summary>
    /// <param name="db">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The classes.</returns>
    internal static async Task<List<CiClass>> LoadAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        await db.Set<CiClass>().AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Label).Take(MaxClasses).ToListAsync(cancellationToken);

    private static CiClassDto ToDto(CiClass c, int count) =>
        new(c.Id, c.Key, c.Label, c.Icon, c.Color, c.ParentKey, c.Description, c.IsSystem, c.SortOrder, CiAttributeSchema.ParseDefinitions(c.AttributesJson), count, c.Version);

    private static string CleanIcon(string? icon)
    {
        var value = icon?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(value) || value.Length > 64 || !value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') ? "box" : value;
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorRegex();
}
