using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Configuration;
using Servora.Domain.Observability;

namespace Servora.Application.Configuration;

/// <summary>Default <see cref="ISecretAdminService"/>: metadata from <c>sys_secret</c>, values through <see cref="ISecretStore"/>.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="secretStore">The encrypted secret store.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class SecretAdminService(
    IAppDbContextFactory dbFactory,
    ISecretStore secretStore,
    ICurrentUser currentUser,
    IAuditService audit,
    IClock clock) : ISecretAdminService
{
    private const int MaxStatisticsRows = 5000;

    /// <inheritdoc />
    public async Task<Result<PagedResult<SecretSummary>>> ListAsync(PageRequest request, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSecrets, cancellationToken))
        {
            return Result<PagedResult<SecretSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<StoredSecret>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(s => s.Name.Contains(term) || (s.Description != null && s.Description.ToLower().Contains(term)));
        }

        query = (request.SortBy, request.SortDescending) switch
        {
            ("ValueSetAt", false) => query.OrderBy(s => s.RotatedAt ?? s.CreatedAt),
            ("ValueSetAt", true) => query.OrderByDescending(s => s.RotatedAt ?? s.CreatedAt),
            ("UpdatedAt", false) => query.OrderBy(s => s.UpdatedAt),
            ("UpdatedAt", true) => query.OrderByDescending(s => s.UpdatedAt),
            (_, true) => query.OrderByDescending(s => s.Name),
            _ => query.OrderBy(s => s.Name),
        };

        var page = await query
            .Select(s => new SecretSummary(s.Id, s.Name, s.Description, s.CreatedAt, s.CreatedBy, s.RotatedAt, s.UpdatedAt, s.UpdatedBy))
            .ToPagedResultAsync(request, cancellationToken);
        return Result<PagedResult<SecretSummary>>.Success(page);
    }

    /// <inheritdoc />
    public async Task<Result<SecretStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSecrets, cancellationToken))
        {
            return Result<SecretStatistics>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<StoredSecret>().AsNoTracking()
            .OrderBy(s => s.Name)
            .Take(MaxStatisticsRows)
            .Select(s => new { s.Name, ValueSetAt = s.RotatedAt ?? s.CreatedAt })
            .ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        return Result<SecretStatistics>.Success(new SecretStatistics(
            rows.Count,
            rows.Count(r => SecretNameRules.IsSystemSecret(r.Name)),
            rows.Count(r => r.ValueSetAt >= now.AddDays(-90)),
            rows.Count(r => r.ValueSetAt < now.AddDays(-SecretNameRules.StaleAfterDays))));
    }

    /// <inheritdoc />
    public async Task<Result> CreateAsync(string name, string? description, string value, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSecrets, cancellationToken))
        {
            return await DeniedAsync("secret.created", name, cancellationToken);
        }

        name = (name ?? string.Empty).Trim();
        var validation = Validate(name, description, value);
        if (validation is not null)
        {
            return validation;
        }

        if (await secretStore.ExistsAsync(name, cancellationToken))
        {
            return Result.Conflict($"A secret named '{name}' already exists. Rotate it instead.");
        }

        await secretStore.SetAsync(name, value, NormalizeDescription(description), cancellationToken);
        await WriteAuditAsync("secret.created", name, $"Secret '{name}' created.", cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RotateAsync(string name, string newValue, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSecrets, cancellationToken))
        {
            return await DeniedAsync("secret.rotated", name, cancellationToken);
        }

        if (string.IsNullOrEmpty(newValue))
        {
            return Result.Invalid("The new value is required.");
        }

        if (newValue.Length > SecretNameRules.MaxValueLength)
        {
            return Result.Invalid($"The value must not exceed {SecretNameRules.MaxValueLength} characters.");
        }

        if (!await secretStore.ExistsAsync(name, cancellationToken))
        {
            return Result.NotFound("The secret was not found.");
        }

        await secretStore.SetAsync(name, newValue, null, cancellationToken);
        await WriteAuditAsync("secret.rotated", name, $"Secret '{name}' rotated.", cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UpdateDescriptionAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSecrets, cancellationToken))
        {
            return await DeniedAsync("secret.updated", name, cancellationToken);
        }

        if (description is { Length: > SecretNameRules.MaxDescriptionLength })
        {
            return Result.Invalid($"The description must not exceed {SecretNameRules.MaxDescriptionLength} characters.");
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var secret = await db.Set<StoredSecret>().FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
            if (secret is null)
            {
                return Result.NotFound("The secret was not found.");
            }

            secret.Description = NormalizeDescription(description);
            await db.SaveChangesAsync(cancellationToken);
        }

        await WriteAuditAsync("secret.updated", name, $"Description of secret '{name}' updated.", cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSecrets, cancellationToken))
        {
            return await DeniedAsync("secret.deleted", name, cancellationToken);
        }

        if (!await secretStore.ExistsAsync(name, cancellationToken))
        {
            return Result.NotFound("The secret was not found.");
        }

        await secretStore.DeleteAsync(name, cancellationToken);
        await WriteAuditAsync("secret.deleted", name, $"Secret '{name}' deleted.", cancellationToken);
        return Result.Success();
    }

    private static Result? Validate(string name, string? description, string value)
    {
        if (!SecretNameRules.IsValidName(name))
        {
            return Result.Invalid("The name must start with a letter and contain only lower-case letters, digits, '.', '-' and '_' (3-200 characters).");
        }

        if (description is { Length: > SecretNameRules.MaxDescriptionLength })
        {
            return Result.Invalid($"The description must not exceed {SecretNameRules.MaxDescriptionLength} characters.");
        }

        if (string.IsNullOrEmpty(value))
        {
            return Result.Invalid("The value is required.");
        }

        return value.Length > SecretNameRules.MaxValueLength
            ? Result.Invalid($"The value must not exceed {SecretNameRules.MaxValueLength} characters.")
            : null;
    }

    private static string? NormalizeDescription(string? description) => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private Task WriteAuditAsync(string action, string name, string message, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, action)
        {
            TargetType = "secret",
            TargetId = name,
            TargetName = name,
            Message = message,
        }, cancellationToken);

    private async Task<Result> DeniedAsync(string action, string? name, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, action, AuditOutcome.Denied)
        {
            TargetType = "secret",
            TargetId = name,
            TargetName = name,
            Message = "Permission denied.",
        }, cancellationToken);
        return Result.Forbidden();
    }
}
