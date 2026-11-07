using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Domain.Api;
using Servora.Domain.Identity;
using Servora.Domain.Observability;

namespace Servora.Application.Api;

/// <summary>
/// Default <see cref="IApiKeyAuthenticator"/>. Key data is cached for a short time (and dropped on every change), the
/// secret is verified with a constant-time hash comparison on every request, failures are audited (throttled so that
/// brute-force attempts cannot flood the audit chain) and usage is aggregated in memory.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="cache">The memory cache.</param>
/// <param name="clock">The clock.</param>
/// <param name="usage">The usage tracker.</param>
/// <param name="rateLimits">The rate limit registry.</param>
/// <param name="audit">The audit service.</param>
public sealed class ApiKeyAuthenticator(
    IAppDbContextFactory dbFactory,
    IMemoryCache cache,
    IClock clock,
    ApiKeyUsageTracker usage,
    ApiKeyRateLimitRegistry rateLimits,
    IAuditService audit) : IApiKeyAuthenticator
{
    /// <summary>The generic failure message returned to clients.</summary>
    public const string InvalidKeyMessage = "The API key is invalid, expired or revoked.";

    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_negativeCacheDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_auditThrottle = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public async Task<ApiKeyAuthenticationResult> AuthenticateAsync(string token, IPAddress? remoteAddress, CancellationToken cancellationToken = default)
    {
        var ip = remoteAddress?.ToString();
        if (!ApiKeyTokens.TryParse(token, out var parsed))
        {
            await AuditFailureAsync("malformed", null, null, ip, cancellationToken);
            return ApiKeyAuthenticationResult.Fail(InvalidKeyMessage);
        }

        var identity = await GetIdentityAsync(parsed.KeyId, cancellationToken);
        if (identity is null)
        {
            await AuditFailureAsync("unknown_key", parsed.KeyId, null, ip, cancellationToken);
            return ApiKeyAuthenticationResult.Fail(InvalidKeyMessage);
        }

        if (!ApiKeyTokens.Verify(parsed.Secret, identity.SecretHash))
        {
            usage.RecordFailure(identity.Id);
            await AuditFailureAsync("invalid_secret", parsed.KeyId, identity, ip, cancellationToken);
            return ApiKeyAuthenticationResult.Fail(InvalidKeyMessage);
        }

        var reason = identity switch
        {
            { RevokedAt: not null } => "revoked",
            { ExpiresAt: { } expires } when expires <= clock.UtcNow => "expired",
            { OwnerActive: false } => "owner_inactive",
            _ when !ApiKeyIpRanges.IsAllowed(identity.AllowedNetworks, remoteAddress) => "ip_not_allowed",
            _ => null,
        };

        if (reason is not null)
        {
            usage.RecordFailure(identity.Id);
            await AuditFailureAsync(reason, parsed.KeyId, identity, ip, cancellationToken);
            return ApiKeyAuthenticationResult.Fail(reason == "ip_not_allowed" ? "The API key is not allowed from this IP address." : InvalidKeyMessage);
        }

        usage.RecordSuccess(identity.Id, ip);
        rateLimits.Set(identity.KeyId, identity.RateLimitPerMinute);
        return ApiKeyAuthenticationResult.Success(identity);
    }

    /// <inheritdoc />
    public void Invalidate(string keyId)
    {
        cache.Remove(CacheKey(keyId));
        rateLimits.Remove(keyId);
    }

    private static string CacheKey(string keyId) => $"servora:apikey:{keyId}";

    private async Task<ApiKeyIdentity?> GetIdentityAsync(string keyId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey(keyId), out ApiKeyIdentity? cached))
        {
            return cached;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var key = await db.Set<ApiKey>().AsNoTracking().FirstOrDefaultAsync(k => k.KeyId == keyId, cancellationToken);
        ApiKeyIdentity? identity = null;
        if (key is not null)
        {
            User? owner = null;
            if (key.OwnerUserId is { } ownerId)
            {
                owner = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == ownerId, cancellationToken);
            }

            identity = new ApiKeyIdentity
            {
                Id = key.Id,
                KeyId = key.KeyId,
                Name = key.Name,
                SecretHash = key.SecretHash,
                OwnerUserId = key.OwnerUserId,
                OwnerUserName = owner?.UserName,
                OwnerDisplayName = owner?.DisplayName,
                OwnerActive = key.OwnerUserId is null || (owner is not null && owner.IsActive),
                ServiceIdentity = key.ServiceIdentity,
                Scopes = ApiKeyScopePolicy.Parse(key.Scopes),
                ExpiresAt = key.ExpiresAt,
                RevokedAt = key.RevokedAt,
                AllowedNetworks = ApiKeyIpRanges.Parse(key.AllowedIpRanges),
                RateLimitPerMinute = key.RateLimitPerMinute,
            };
        }

        cache.Set(CacheKey(keyId), identity, identity is null ? s_negativeCacheDuration : s_cacheDuration);
        return identity;
    }

    private async Task AuditFailureAsync(string reason, string? keyId, ApiKeyIdentity? identity, string? ip, CancellationToken cancellationToken)
    {
        // At most one audit event per reason and key (or client address) per minute.
        var throttleKey = $"servora:apikey-audit:{reason}:{keyId ?? ip ?? "unknown"}";
        if (cache.TryGetValue(throttleKey, out _))
        {
            return;
        }

        cache.Set(throttleKey, true, s_auditThrottle);
        await audit.WriteAsync(
            new AuditEntry("auth", "apikey.auth_failed", AuditOutcome.Failure)
            {
                TargetType = "api_key",
                TargetId = identity?.Id.ToString(),
                TargetName = identity?.Name,
                Message = $"API key authentication failed: {reason.Replace('_', ' ')}.",
                Details = new { reason, keyId },
                ActorNameOverride = keyId is null ? "apikey:unknown" : $"apikey:{keyId}",
            },
            cancellationToken);
    }
}
