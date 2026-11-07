using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Identity;
using Servora.Application.Identity.Ldap;
using Servora.Domain.Identity;
using Servora.Infrastructure.Identity;

namespace Servora.Tests.Unit.Identity;

/// <summary>Mapping of Windows identities to Servora accounts by <see cref="WindowsSignInService"/>.</summary>
public sealed class WindowsSignInServiceTests : IDisposable
{
    private const string JaneSid = "S-1-5-21-1004336348-1177238915-682003330-1105";

    private readonly IdentityTestDatabase _db = new();
    private readonly RecordingAuditService _audit = new();
    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public async Task SignIn_WhenWindowsAuthIsDisabled_Fails()
    {
        var service = CreateService(new DirectorySettings { WindowsAuthEnabled = false });

        var result = await service.SignInAsync(@"CORP\jane", JaneSid);

        Assert.False(result.Succeeded);
        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        Assert.Contains(_audit.Entries, e => e.Action == "login.failed");
        Assert.False(await AnyUserAsync());
    }

    [Fact]
    public async Task SignIn_FromForeignDomain_IsRejectedWithoutDirectoryLookup()
    {
        var client = new FakeDirectoryClient(new DirectoryLookupResult(DirectoryLookupOutcome.Success, Entry("jane", JaneSid)));
        var service = CreateService(DirectorySettingsWith(ldap: true), client);

        var result = await service.SignInAsync(@"EVIL\jane", JaneSid);

        Assert.False(result.Succeeded);
        Assert.Equal(0, client.Lookups);
    }

    [Fact]
    public async Task SignIn_WithoutDirectory_AutoProvisionsWindowsAccountBySid_AndReusesIt()
    {
        var defaultRole = await AddRoleAsync("user");
        var service = CreateService(DirectorySettingsWith(ldap: false));

        var first = await service.SignInAsync(@"CORP\jane", JaneSid);
        var second = await service.SignInAsync(@"corp\jane", JaneSid);

        Assert.True(first.Succeeded, first.Message);
        Assert.Equal(WindowsSignInService.SourceKey, first.AuthSource);
        Assert.Equal(first.User!.Id, second.User!.Id);
        await using var db = await _db.CreateDbContextAsync();
        var user = await db.Set<User>().Include(u => u.Roles).SingleAsync();
        Assert.Equal(UserSource.Windows, user.Source);
        Assert.Equal(JaneSid, user.ExternalId);
        Assert.Null(user.PasswordHash);
        Assert.Contains(user.Roles, r => r.RoleId == defaultRole);
        Assert.Single(_audit.Entries, e => e.Action == "user.provisioned");
    }

    [Fact]
    public async Task SignIn_AutoProvisioningWithoutSid_Fails()
    {
        var service = CreateService(DirectorySettingsWith(ldap: false));

        var result = await service.SignInAsync(@"CORP\jane", null);

        Assert.False(result.Succeeded);
        Assert.False(await AnyUserAsync());
    }

    [Fact]
    public async Task SignIn_NeverTakesOverALocalAccountWithTheSameName()
    {
        await AddUserAsync("admin", UserSource.Local);
        var service = CreateService(DirectorySettingsWith(ldap: false));

        var result = await service.SignInAsync(@"CORP\admin", JaneSid);

        Assert.False(result.Succeeded);
        await using var db = await _db.CreateDbContextAsync();
        Assert.Equal(UserSource.Local, (await db.Set<User>().SingleAsync()).Source);
    }

    [Fact]
    public async Task SignIn_WhenDirectoryIsUnavailable_FallsBackToExistingDirectoryAccount()
    {
        var existing = await AddUserAsync("jane", UserSource.Directory);
        var client = new FakeDirectoryClient(new DirectoryLookupResult(DirectoryLookupOutcome.Unavailable));
        var service = CreateService(DirectorySettingsWith(ldap: true), client);

        var result = await service.SignInAsync(@"CORP\jane", JaneSid);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(existing, result.User!.Id);
        Assert.Equal(1, client.Lookups);
    }

    [Fact]
    public async Task SignIn_WhenDirectoryDoesNotKnowTheUser_DoesNotUseStaleAccount()
    {
        await AddUserAsync("jane", UserSource.Directory);
        var client = new FakeDirectoryClient(new DirectoryLookupResult(DirectoryLookupOutcome.UserNotFound));
        var service = CreateService(DirectorySettingsWith(ldap: true), client);

        var result = await service.SignInAsync(@"CORP\jane", JaneSid);

        Assert.False(result.Succeeded);
        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
    }

    [Fact]
    public async Task SignIn_WithSidDifferentFromDirectoryAccount_IsRefused()
    {
        var client = new FakeDirectoryClient(new DirectoryLookupResult(DirectoryLookupOutcome.Success, Entry("jane", "S-1-5-21-1-2-3-999")));
        var service = CreateService(DirectorySettingsWith(ldap: true), client);

        var result = await service.SignInAsync(@"CORP\jane", JaneSid);

        Assert.False(result.Succeeded);
        Assert.Contains(_audit.Entries, e => e.Action == "login.failed" && e.Message!.Contains("SID", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignIn_DeactivatedWindowsAccount_ReportsDisabled()
    {
        await AddUserAsync("jane", UserSource.Windows, JaneSid, active: false);
        var service = CreateService(DirectorySettingsWith(ldap: false));

        var result = await service.SignInAsync(@"CORP\jane", JaneSid);

        Assert.Equal(SignInOutcome.Disabled, result.Outcome);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _services.Dispose();
        _db.Dispose();
    }

    private static DirectorySettings DirectorySettingsWith(bool ldap) => new()
    {
        WindowsAuthEnabled = true,
        WindowsAutoProvision = true,
        NetBiosDomain = "CORP",
        Enabled = ldap,
        Host = ldap ? "dc01.corp.example.com" : string.Empty,
        BaseDn = ldap ? "DC=corp,DC=example,DC=com" : string.Empty,
        BindDn = ldap ? "CN=svc-servora,OU=Service,DC=corp,DC=example,DC=com" : string.Empty,
        DefaultRoleName = "user",
    };

    private static DirectoryUserEntry Entry(string userName, string sid) => new(
        $"CN={userName},OU=Users,DC=corp,DC=example,DC=com",
        userName,
        "Jane Doe",
        "Jane",
        "Doe",
        $"{userName}@corp.example.com",
        null,
        null,
        null,
        $"guid-{userName}",
        sid,
        false,
        []);

    private WindowsSignInService CreateService(DirectorySettings settings, FakeDirectoryClient? client = null) => new(
        new FakeDirectoryConfigurationProvider(settings),
        client ?? new FakeDirectoryClient(new DirectoryLookupResult(DirectoryLookupOutcome.ConfigurationError)),
        new SystemScopeProvisioner(_services.GetRequiredService<IServiceScopeFactory>()),
        _db,
        _audit,
        new FixedClock());

    private async Task<bool> AnyUserAsync()
    {
        await using var db = await _db.CreateDbContextAsync();
        return await db.Set<User>().AnyAsync();
    }

    private async Task<Guid> AddRoleAsync(string name)
    {
        await using var db = await _db.CreateDbContextAsync();
        var role = new Role { Name = name, Label = name };
        db.Set<Role>().Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<Guid> AddUserAsync(string userName, UserSource source, string? externalId = null, bool active = true)
    {
        await using var db = await _db.CreateDbContextAsync();
        var user = new User
        {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = userName,
            Source = source,
            ExternalId = externalId,
            IsActive = active,
            PasswordHash = source == UserSource.Local ? "hash" : null,
        };
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
