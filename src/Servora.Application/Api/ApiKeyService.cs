using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Api;
using Servora.Domain.Identity;
using Servora.Domain.Observability;

namespace Servora.Application.Api;

/// <summary>Default <see cref="IApiKeyService"/>.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="permissions">The permission service.</param>
/// <param name="authenticator">The authenticator (cache invalidation).</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
/// <param name="tools">The registered MCP tools (for tool scopes).</param>
public sealed partial class ApiKeyService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IApiKeyAuthenticator authenticator,
    IAuditService audit,
    IClock clock,
    IEnumerable<IMcpTool> tools) : IApiKeyService
{
    private const string AuditCategory = "security";

    /// <inheritdoc />
    public async Task<Result<PagedResult<ApiKeySummary>>> ListAsync(PageRequest page, ApiKeyStatus? status = null, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<PagedResult<ApiKeySummary>>.Forbidden();
        }

        var now = clock.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<ApiKey>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(k => k.Name.Contains(term) || k.KeyId.Contains(term) || (k.Description != null && k.Description.Contains(term))
                                     || (k.ServiceIdentity != null && k.ServiceIdentity.Contains(term)));
        }

        query = status switch
        {
            ApiKeyStatus.Active => query.Where(k => k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > now)),
            ApiKeyStatus.Expired => query.Where(k => k.RevokedAt == null && k.ExpiresAt != null && k.ExpiresAt <= now),
            ApiKeyStatus.Revoked => query.Where(k => k.RevokedAt != null),
            _ => query,
        };

        query = (page.SortBy, page.SortDescending) switch
        {
            ("Name", false) => query.OrderBy(k => k.Name),
            ("Name", true) => query.OrderByDescending(k => k.Name),
            ("LastUsedAt", false) => query.OrderBy(k => k.LastUsedAt),
            ("LastUsedAt", true) => query.OrderByDescending(k => k.LastUsedAt),
            ("ExpiresAt", false) => query.OrderBy(k => k.ExpiresAt),
            ("ExpiresAt", true) => query.OrderByDescending(k => k.ExpiresAt),
            ("CreatedAt", false) => query.OrderBy(k => k.CreatedAt),
            _ => query.OrderByDescending(k => k.CreatedAt),
        };

        var keys = await query.ToPagedResultAsync(page, cancellationToken);
        var owners = await LoadOwnerNamesAsync(db, keys.Items.Select(k => k.OwnerUserId), cancellationToken);
        var items = keys.Items.Select(k => new ApiKeySummary(
            k.Id,
            k.Name,
            ApiKeyTokens.Mask(k.KeyId),
            OwnerName(k, owners),
            k.OwnerUserId is null,
            ApiKeyScopePolicy.Parse(k.Scopes).Count,
            StatusOf(k, now),
            k.CreatedAt,
            k.ExpiresAt,
            k.LastUsedAt,
            k.UsageCount,
            k.RateLimitPerMinute)).ToList();
        return Result<PagedResult<ApiKeySummary>>.Success(new PagedResult<ApiKeySummary>(items, keys.TotalCount, keys.Page, keys.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<ApiKeyDetails>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<ApiKeyDetails>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var key = await db.Set<ApiKey>().AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (key is null)
        {
            return Result<ApiKeyDetails>.NotFound("The API key was not found.");
        }

        var owners = await LoadOwnerNamesAsync(db, [key.OwnerUserId], cancellationToken);
        return Result<ApiKeyDetails>.Success(new ApiKeyDetails
        {
            Id = key.Id,
            Name = key.Name,
            Description = key.Description,
            KeyId = key.KeyId,
            KeyPreview = ApiKeyTokens.Mask(key.KeyId),
            OwnerUserId = key.OwnerUserId,
            OwnerName = OwnerName(key, owners),
            ServiceIdentity = key.ServiceIdentity,
            Scopes = ApiKeyScopePolicy.Parse(key.Scopes),
            AllowedIpRanges = ApiKeyIpRanges.Parse(key.AllowedIpRanges).Select(n => n.ToString()).ToList(),
            RateLimitPerMinute = key.RateLimitPerMinute,
            Status = StatusOf(key, clock.UtcNow),
            CreatedAt = key.CreatedAt,
            CreatedBy = key.CreatedBy,
            ExpiresAt = key.ExpiresAt,
            RotatedAt = key.RotatedAt,
            RevokedAt = key.RevokedAt,
            RevokedBy = key.RevokedBy,
            RevocationReason = key.RevocationReason,
            LastUsedAt = key.LastUsedAt,
            LastUsedIp = key.LastUsedIp,
            UsageCount = key.UsageCount,
        });
    }

    /// <inheritdoc />
    public async Task<Result<ApiKeyStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<ApiKeyStats>.Forbidden();
        }

        var now = clock.UtcNow;
        var soon = now + ApiKeyLimits.ExpiringSoonWindow;
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var from = today.AddDays(-13);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var keys = db.Set<ApiKey>().AsNoTracking();
        var active = await keys.CountAsync(k => k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > now), cancellationToken);
        var expiring = await keys.CountAsync(k => k.RevokedAt == null && k.ExpiresAt != null && k.ExpiresAt > now && k.ExpiresAt <= soon, cancellationToken);
        var revoked = await keys.CountAsync(k => k.RevokedAt != null, cancellationToken);

        var usage = await db.Set<ApiKeyUsageDay>().AsNoTracking()
            .Where(u => u.Day >= from)
            .Select(u => new { u.Day, u.RequestCount, u.FailedCount })
            .ToListAsync(cancellationToken);
        var daily = Enumerable.Range(0, 14)
            .Select(i => from.AddDays(i))
            .Select(day => new ApiKeyUsagePoint(
                day,
                usage.Where(u => u.Day == day).Sum(u => u.RequestCount),
                usage.Where(u => u.Day == day).Sum(u => u.FailedCount)))
            .ToList();
        var todayPoint = daily[^1];
        return Result<ApiKeyStats>.Success(new ApiKeyStats(active, expiring, revoked, todayPoint.Requests, todayPoint.Failed, daily));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ApiKeyUsagePoint>>> GetUsageAsync(Guid id, int days = 30, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<ApiKeyUsagePoint>>.Forbidden();
        }

        days = Math.Clamp(days, 1, 90);
        var today = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var from = today.AddDays(-(days - 1));
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<ApiKeyUsageDay>().AsNoTracking()
            .Where(u => u.ApiKeyId == id && u.Day >= from)
            .ToListAsync(cancellationToken);
        IReadOnlyList<ApiKeyUsagePoint> points = Enumerable.Range(0, days)
            .Select(i => from.AddDays(i))
            .Select(day => rows.FirstOrDefault(r => r.Day == day) is { } r
                ? new ApiKeyUsagePoint(day, r.RequestCount, r.FailedCount)
                : new ApiKeyUsagePoint(day, 0, 0))
            .ToList();
        return Result<IReadOnlyList<ApiKeyUsagePoint>>.Success(points);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GrantableScope>>> GetGrantableScopesAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken) || currentUser.UserId is not { } userId)
        {
            return Result<IReadOnlyList<GrantableScope>>.Forbidden();
        }

        var granted = await permissions.GetUserPermissionsAsync(userId, cancellationToken);
        var scopes = PermissionCatalog.All
            .Where(p => ApiKeyScopePolicy.IsGrantable(granted, p.Key))
            .Select(p => new GrantableScope(p.Key, p.Group, p.Description))
            .ToList();

        if (ApiKeyScopePolicy.IsGrantable(granted, McpScopes.AllTools))
        {
            scopes.Add(new GrantableScope(McpScopes.AllTools, "MCP Tools", "Call every MCP tool (the underlying permissions still apply)."));
            scopes.AddRange(tools
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .Select(t => new GrantableScope(McpScopes.ForTool(t.Name), "MCP Tools", $"Call the MCP tool '{t.Name}': {t.Title}.")));
        }

        return Result<IReadOnlyList<GrantableScope>>.Success(scopes);
    }

    /// <inheritdoc />
    public async Task<Result<ApiKeySecretResult>> CreateAsync(ApiKeyCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<ApiKeySecretResult>.Forbidden();
        }

        // Keys are created by interactive users only, so a key can never mint further keys.
        if (currentUser.ActorType != ActorType.User || currentUser.UserId is not { } creatorId)
        {
            return Result<ApiKeySecretResult>.Forbidden("API keys can only be created by signed-in users.");
        }

        var creatorPermissions = await permissions.GetUserPermissionsAsync(creatorId, cancellationToken);
        var validation = ValidateSettings(request.Name, request.Description, request.ExpiresAt, request.RateLimitPerMinute);
        if (!validation.Succeeded)
        {
            return Result<ApiKeySecretResult>.From(validation);
        }

        var scopes = ApiKeyScopePolicy.Validate(creatorPermissions, request.Scopes);
        if (!scopes.Succeeded)
        {
            return Result<ApiKeySecretResult>.From(scopes);
        }

        var ipRanges = ApiKeyIpRanges.Normalize(request.AllowedIpRanges);
        if (!ipRanges.Succeeded)
        {
            return Result<ApiKeySecretResult>.From(ipRanges);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        string? serviceIdentity = null;
        string ownerName;
        if (request.OwnerUserId is { } ownerId)
        {
            if (ownerId != creatorId && !PermissionMatcher.IsGranted(creatorPermissions, Permissions.Identity.ManageUsers))
            {
                return Result<ApiKeySecretResult>.Forbidden("Only user administrators can create keys owned by other users.");
            }

            var owner = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == ownerId, cancellationToken);
            if (owner is null || !owner.IsActive)
            {
                return Result<ApiKeySecretResult>.Invalid("The owner must be an active user.");
            }

            ownerName = owner.DisplayName;
        }
        else
        {
            serviceIdentity = request.ServiceIdentity?.Trim();
            if (string.IsNullOrEmpty(serviceIdentity) || !ServiceIdentitySyntax().IsMatch(serviceIdentity))
            {
                return Result<ApiKeySecretResult>.Invalid("Enter a service identity name (letters, digits, spaces, dots, dashes; max 100 characters) or choose an owner user.");
            }

            ownerName = serviceIdentity;
        }

        var token = ApiKeyTokens.Generate();
        while (await db.Set<ApiKey>().AnyAsync(k => k.KeyId == token.KeyId, cancellationToken))
        {
            token = ApiKeyTokens.Generate();
        }

        var key = new ApiKey
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            KeyId = token.KeyId,
            SecretHash = token.SecretHash,
            OwnerUserId = request.OwnerUserId,
            ServiceIdentity = serviceIdentity,
            Scopes = ApiKeyScopePolicy.Format(scopes.Value!),
            ExpiresAt = request.ExpiresAt,
            AllowedIpRanges = ipRanges.Value,
            RateLimitPerMinute = request.RateLimitPerMinute,
        };
        db.Set<ApiKey>().Add(key);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            new AuditEntry(AuditCategory, "apikey.created")
            {
                TargetType = "api_key",
                TargetId = key.Id.ToString(),
                TargetName = key.Name,
                Message = $"API key '{key.Name}' created for {ownerName}.",
                Details = new { key.KeyId, owner = ownerName, scopes = scopes.Value, key.ExpiresAt, ipRanges = key.AllowedIpRanges, key.RateLimitPerMinute },
            },
            cancellationToken);

        return Result<ApiKeySecretResult>.Success(new ApiKeySecretResult(key.Id, key.Name, token.Token));
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, ApiKeyUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        if (currentUser.ActorType != ActorType.User || currentUser.UserId is not { } editorId)
        {
            return Result.Forbidden("API keys can only be changed by signed-in users.");
        }

        var validation = ValidateSettings(request.Name, request.Description, request.ExpiresAt, request.RateLimitPerMinute);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var ipRanges = ApiKeyIpRanges.Normalize(request.AllowedIpRanges);
        if (!ipRanges.Succeeded)
        {
            return ipRanges;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var key = await db.Set<ApiKey>().FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (key is null)
        {
            return Result.NotFound("The API key was not found.");
        }

        if (key.RevokedAt is not null)
        {
            return Result.Conflict("Revoked keys cannot be changed.");
        }

        // Scopes that the key already has may be kept; newly added scopes must be grantable by the editor.
        var editorPermissions = await permissions.GetUserPermissionsAsync(editorId, cancellationToken);
        var existing = ApiKeyScopePolicy.Parse(key.Scopes).ToHashSet(StringComparer.Ordinal);
        var requested = ApiKeyScopePolicy.Parse(string.Join(' ', request.Scopes));
        var added = requested.Where(s => !existing.Contains(s)).ToList();
        if (requested.Count == 0)
        {
            return Result.Invalid("Select at least one scope.");
        }

        if (added.Count > 0)
        {
            var addedValidation = ApiKeyScopePolicy.Validate(editorPermissions, added);
            if (!addedValidation.Succeeded)
            {
                return addedValidation;
            }
        }

        var removed = existing.Where(s => !requested.Contains(s)).ToList();
        key.Name = request.Name.Trim();
        key.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        key.Scopes = ApiKeyScopePolicy.Format(requested);
        key.ExpiresAt = request.ExpiresAt;
        key.AllowedIpRanges = ipRanges.Value;
        key.RateLimitPerMinute = request.RateLimitPerMinute;
        await db.SaveChangesAsync(cancellationToken);
        authenticator.Invalidate(key.KeyId);

        await audit.WriteAsync(
            new AuditEntry(AuditCategory, "apikey.updated")
            {
                TargetType = "api_key",
                TargetId = key.Id.ToString(),
                TargetName = key.Name,
                Message = $"API key '{key.Name}' updated.",
                Details = new { addedScopes = added, removedScopes = removed, key.ExpiresAt, ipRanges = key.AllowedIpRanges, key.RateLimitPerMinute },
            },
            cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<ApiKeySecretResult>> RotateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<ApiKeySecretResult>.Forbidden();
        }

        if (currentUser.ActorType != ActorType.User)
        {
            return Result<ApiKeySecretResult>.Forbidden("API keys can only be rotated by signed-in users.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var key = await db.Set<ApiKey>().FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (key is null)
        {
            return Result<ApiKeySecretResult>.NotFound("The API key was not found.");
        }

        if (key.RevokedAt is not null)
        {
            return Result<ApiKeySecretResult>.Conflict("Revoked keys cannot be rotated. Create a new key instead.");
        }

        var token = ApiKeyTokens.GenerateForKeyId(key.KeyId);
        key.SecretHash = token.SecretHash;
        key.RotatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        authenticator.Invalidate(key.KeyId);

        await audit.WriteAsync(
            new AuditEntry(AuditCategory, "apikey.rotated")
            {
                TargetType = "api_key",
                TargetId = key.Id.ToString(),
                TargetName = key.Name,
                Message = $"Secret of API key '{key.Name}' rotated; the previous secret is no longer valid.",
            },
            cancellationToken);
        return Result<ApiKeySecretResult>.Success(new ApiKeySecretResult(key.Id, key.Name, token.Token));
    }

    /// <inheritdoc />
    public async Task<Result> RevokeAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var key = await db.Set<ApiKey>().FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (key is null)
        {
            return Result.NotFound("The API key was not found.");
        }

        if (key.RevokedAt is not null)
        {
            return Result.Conflict("The API key is already revoked.");
        }

        key.RevokedAt = clock.UtcNow;
        key.RevokedBy = currentUser.DisplayName;
        key.RevocationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
        await db.SaveChangesAsync(cancellationToken);
        authenticator.Invalidate(key.KeyId);

        await audit.WriteAsync(
            new AuditEntry(AuditCategory, "apikey.revoked")
            {
                TargetType = "api_key",
                TargetId = key.Id.ToString(),
                TargetName = key.Name,
                Message = $"API key '{key.Name}' revoked.",
                Details = new { reason = key.RevocationReason },
            },
            cancellationToken);
        return Result.Success();
    }

    /// <summary>Computes the effective status of a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="now">The current UTC time.</param>
    /// <returns>The status.</returns>
    internal static ApiKeyStatus StatusOf(ApiKey key, DateTimeOffset now) =>
        key.RevokedAt is not null ? ApiKeyStatus.Revoked
        : key.ExpiresAt is { } expires && expires <= now ? ApiKeyStatus.Expired
        : ApiKeyStatus.Active;

    private static string OwnerName(ApiKey key, IReadOnlyDictionary<Guid, string> owners) =>
        key.OwnerUserId is { } ownerId ? owners.GetValueOrDefault(ownerId, "Unknown user") : key.ServiceIdentity ?? "Service identity";

    private static async Task<IReadOnlyDictionary<Guid, string>> LoadOwnerNamesAsync(IAppDbContext db, IEnumerable<Guid?> ownerIds, CancellationToken cancellationToken)
    {
        var ids = ownerIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Set<User>().AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
    }

    private Result ValidateSettings(string name, string? description, DateTimeOffset? expiresAt, int rateLimit)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            return Result.Invalid("Enter a name with at most 100 characters.");
        }

        if (description is { Length: > 1000 })
        {
            return Result.Invalid("The description can have at most 1000 characters.");
        }

        var now = clock.UtcNow;
        if (expiresAt is { } expires && (expires <= now || expires > now + ApiKeyLimits.MaxLifetime))
        {
            return Result.Invalid("The expiry date must be in the future and at most three years ahead.");
        }

        if (rateLimit is < 1 or > ApiKeyLimits.MaxRateLimitPerMinute)
        {
            return Result.Invalid($"The rate limit must be between 1 and {ApiKeyLimits.MaxRateLimitPerMinute:N0} requests per minute.");
        }

        return Result.Success();
    }

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) => currentUser.HasPermissionAsync(Permissions.Api.ManageApiKeys, cancellationToken);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 ._\-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex ServiceIdentitySyntax();
}
