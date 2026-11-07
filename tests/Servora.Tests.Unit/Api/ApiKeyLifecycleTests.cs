using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Api;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Security;
using Servora.Domain.Api;
using Servora.Domain.Identity;
using Servora.Tests.Unit.Api.Fakes;

namespace Servora.Tests.Unit.Api;

/// <summary>
/// End-to-end tests of <see cref="ApiKeyService"/> and <see cref="ApiKeyAuthenticator"/> on SQLite: create → authenticate
/// → rotate → revoke, scope limits, IP restrictions and audit.
/// </summary>
public sealed class ApiKeyLifecycleTests : IAsyncLifetime
{
    private readonly TestCurrentUser _user = new();
    private readonly TestPermissionService _permissions = new();
    private readonly RecordingAuditService _audit = new();
    private readonly TestClock _clock = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private TestDatabase _db = null!;
    private ApiKeyAuthenticator _authenticator = null!;
    private ApiKeyService _service = null!;

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();
        await using (var db = _db.Create())
        {
            db.Set<User>().Add(new User { Id = _user.UserId!.Value, UserName = "admin", NormalizedUserName = "ADMIN", DisplayName = "Ada Admin" });
            await db.SaveChangesAsync();
        }

        _permissions.Set(_user.UserId!.Value, Permissions.All);
        _authenticator = new ApiKeyAuthenticator(_db, _cache, _clock, new ApiKeyUsageTracker(_clock), new ApiKeyRateLimitRegistry(), _audit);
        _service = new ApiKeyService(_db, _user, _permissions, _authenticator, _audit, _clock, Array.Empty<IMcpTool>());
    }

    public async Task DisposeAsync()
    {
        _cache.Dispose();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Create_ReturnsTheTokenOnceAndStoresOnlyTheHash()
    {
        var created = await CreateKeyAsync();

        Assert.True(ApiKeyTokens.TryParse(created.Token, out var parsed));
        await using var db = _db.Create();
        var stored = await db.Set<ApiKey>().SingleAsync();
        Assert.Equal(parsed.KeyId, stored.KeyId);
        Assert.Equal(ApiKeyTokens.HashSecret(parsed.Secret), stored.SecretHash);
        Assert.DoesNotContain(parsed.Secret, stored.SecretHash, StringComparison.Ordinal);

        var details = (await _service.GetAsync(created.Id)).Value!;
        Assert.DoesNotContain(parsed.Secret, System.Text.Json.JsonSerializer.Serialize(details), StringComparison.Ordinal);
        Assert.Contains(_audit.Entries, e => e.Action == "apikey.created");
        Assert.DoesNotContain(created.Token, created.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authenticate_SucceedsForValidTokenWithScopesAndOwner()
    {
        var created = await CreateKeyAsync();

        var result = await _authenticator.AuthenticateAsync(created.Token, IPAddress.Loopback);

        Assert.True(result.Succeeded, result.Failure);
        Assert.Equal(_user.UserId, result.Identity!.OwnerUserId);
        Assert.Equal(new[] { Permissions.Api.Access, Permissions.Incidents.Read }.Order(StringComparer.Ordinal), result.Identity.Scopes);
    }

    [Fact]
    public async Task Authenticate_FailsForWrongSecretAndAuditsTheFailure()
    {
        var created = await CreateKeyAsync();
        ApiKeyTokens.TryParse(created.Token, out var parsed);
        var forged = $"srv_{parsed!.KeyId}_{new string('x', ApiKeyTokens.SecretLength)}";

        var result = await _authenticator.AuthenticateAsync(forged, IPAddress.Loopback);

        Assert.False(result.Succeeded);
        Assert.Equal(ApiKeyAuthenticator.InvalidKeyMessage, result.Failure);
        Assert.Contains(_audit.Entries, e => e.Action == "apikey.auth_failed");
    }

    [Fact]
    public async Task Revoke_StopsTheKeyImmediately()
    {
        var created = await CreateKeyAsync();
        Assert.True((await _authenticator.AuthenticateAsync(created.Token, IPAddress.Loopback)).Succeeded);

        var revoked = await _service.RevokeAsync(created.Id, "leaked");

        Assert.True(revoked.Succeeded, revoked.Error);
        Assert.False((await _authenticator.AuthenticateAsync(created.Token, IPAddress.Loopback)).Succeeded);
        Assert.Equal(ResultErrorKind.Conflict, (await _service.RotateAsync(created.Id)).ErrorKind);
        Assert.Contains(_audit.Entries, e => e.Action == "apikey.revoked");
    }

    [Fact]
    public async Task Rotate_InvalidatesTheOldTokenAndIssuesANewOne()
    {
        var created = await CreateKeyAsync();
        Assert.True((await _authenticator.AuthenticateAsync(created.Token, IPAddress.Loopback)).Succeeded);

        var rotated = await _service.RotateAsync(created.Id);

        Assert.True(rotated.Succeeded, rotated.Error);
        Assert.NotEqual(created.Token, rotated.Value!.Token);
        Assert.False((await _authenticator.AuthenticateAsync(created.Token, IPAddress.Loopback)).Succeeded);
        Assert.True((await _authenticator.AuthenticateAsync(rotated.Value.Token, IPAddress.Loopback)).Succeeded);
    }

    [Fact]
    public async Task Authenticate_RejectsExpiredKeys()
    {
        var created = await CreateKeyAsync(expiresAt: _clock.UtcNow.AddDays(1));
        _clock.UtcNow = _clock.UtcNow.AddDays(2);

        Assert.False((await _authenticator.AuthenticateAsync(created.Token, IPAddress.Loopback)).Succeeded);
    }

    [Fact]
    public async Task Authenticate_EnforcesAllowedIpRanges()
    {
        var created = await CreateKeyAsync(ipRanges: "10.0.0.0/8");

        Assert.True((await _authenticator.AuthenticateAsync(created.Token, IPAddress.Parse("10.1.2.3"))).Succeeded);
        var outside = await _authenticator.AuthenticateAsync(created.Token, IPAddress.Parse("192.168.0.1"));
        Assert.False(outside.Succeeded);
        Assert.Contains("IP address", outside.Failure);
    }

    [Fact]
    public async Task Authenticate_RejectsKeysOfDeactivatedOwners()
    {
        var created = await CreateKeyAsync();
        await using (var db = _db.Create())
        {
            var owner = await db.Set<User>().SingleAsync();
            owner.IsActive = false;
            await db.SaveChangesAsync();
        }

        _authenticator.Invalidate(ApiKeyTokens.TryGetKeyId(created.Token)!);

        Assert.False((await _authenticator.AuthenticateAsync(created.Token, IPAddress.Loopback)).Succeeded);
    }

    [Fact]
    public async Task Create_CannotGrantScopesBeyondTheCreatorsPermissions()
    {
        _permissions.Set(_user.UserId!.Value, Permissions.Api.ManageApiKeys, Permissions.Api.Access, Permissions.Incidents.Read);

        var result = await _service.CreateAsync(new ApiKeyCreateRequest
        {
            Name = "Escalation",
            OwnerUserId = _user.UserId,
            Scopes = [Permissions.Incidents.Read, Permissions.Incidents.Delete],
        });

        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task Service_RequiresTheManagePermission()
    {
        _user.Grant(Permissions.Api.Access);

        Assert.Equal(ResultErrorKind.Forbidden, (await _service.ListAsync(new PageRequest())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _service.CreateAsync(new ApiKeyCreateRequest { Name = "x", ServiceIdentity = "svc", Scopes = ["api.access"] })).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _service.RevokeAsync(Guid.NewGuid(), null)).ErrorKind);
    }

    [Fact]
    public async Task Create_ServiceIdentityKeyWithoutOwner()
    {
        var result = await _service.CreateAsync(new ApiKeyCreateRequest { Name = "Monitoring", ServiceIdentity = "zabbix", Scopes = [Permissions.Api.Access] });

        Assert.True(result.Succeeded, result.Error);
        var auth = await _authenticator.AuthenticateAsync(result.Value!.Token, IPAddress.Loopback);
        Assert.True(auth.Succeeded);
        Assert.Null(auth.Identity!.OwnerUserId);
        Assert.Equal("svc:zabbix", auth.Identity.PrincipalName);
    }

    private async Task<ApiKeySecretResult> CreateKeyAsync(DateTimeOffset? expiresAt = null, string? ipRanges = null)
    {
        var result = await _service.CreateAsync(new ApiKeyCreateRequest
        {
            Name = "Integration",
            OwnerUserId = _user.UserId,
            Scopes = [Permissions.Incidents.Read, Permissions.Api.Access],
            ExpiresAt = expiresAt,
            AllowedIpRanges = ipRanges,
        });
        Assert.True(result.Succeeded, result.Error);
        return result.Value!;
    }
}
