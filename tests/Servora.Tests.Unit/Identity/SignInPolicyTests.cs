using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Identity;
using Servora.Application.Identity.Policies;
using Servora.Application.Security;
using Servora.Domain.Identity;
using Servora.Infrastructure.Security;

namespace Servora.Tests.Unit.Identity;

/// <summary>Authentication policy effects on <see cref="SignInService"/>: local sign-in switch with break-glass admin and lockout threshold.</summary>
public sealed class SignInPolicyTests : IDisposable
{
    private const string Password = "Correct-Horse-42";
    private readonly IdentityTestDatabase _db = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly PasswordHasherService _hasher = new();
    private readonly RecordingAuditService _audit = new();

    [Fact]
    public async Task LocalSignInDisabled_RejectsLocalUsers_ButAllowsBootstrapAdmin()
    {
        await SeedUserAsync("jane.doe");
        await SeedUserAsync(BootstrapAccount.AdminUserName);
        await SavePolicyAsync(new AuthPolicySettings { AllowLocalLogin = false });
        var service = CreateService();

        var jane = await service.ValidateCredentialsAsync("jane.doe", Password);
        var admin = await service.ValidateCredentialsAsync("admin", Password);

        Assert.False(jane.Succeeded);
        Assert.Equal(SignInOutcome.InvalidCredentials, jane.Outcome);
        Assert.True(admin.Succeeded);
    }

    [Fact]
    public async Task LockoutThresholdComesFromPolicy()
    {
        await SeedUserAsync("jane.doe");
        await SavePolicyAsync(new AuthPolicySettings { MaxFailedAttempts = 2, LockoutMinutes = 5 });
        var service = CreateService();

        await service.ValidateCredentialsAsync("jane.doe", "wrong-1");
        await service.ValidateCredentialsAsync("jane.doe", "wrong-2");
        var locked = await service.ValidateCredentialsAsync("jane.doe", Password);

        Assert.Equal(SignInOutcome.LockedOut, locked.Outcome);
        Assert.Contains(_audit.Entries, e => e.Action == "account.locked");
    }

    [Fact]
    public async Task PasswordChangeUsesConfiguredMinimumLength()
    {
        var userId = await SeedUserAsync("jane.doe");
        await SavePolicyAsync(new AuthPolicySettings { MinimumPasswordLength = 20 });

        var result = await CreateService().ChangePasswordAsync(userId, Password, "Short-But-Ok-123");

        Assert.False(result.Succeeded);
        Assert.Contains("20", result.Error, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
    }

    private SignInService CreateService() => new(
        _db,
        _hasher,
        [],
        _audit,
        new FixedClock(),
        new PermissionService(_db, _cache),
        new AuthPolicyProvider(new IdentitySettingsStore(_db, _cache)));

    private Task SavePolicyAsync(AuthPolicySettings settings) =>
        new IdentitySettingsStore(_db, _cache).SaveAsync(AuthPolicySettings.SettingKey, settings, "test");

    private async Task<Guid> SeedUserAsync(string userName)
    {
        await using var db = await _db.CreateDbContextAsync();
        var user = new User { UserName = userName, NormalizedUserName = userName.ToUpperInvariant(), DisplayName = userName, PasswordHash = _hasher.Hash(Password) };
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
