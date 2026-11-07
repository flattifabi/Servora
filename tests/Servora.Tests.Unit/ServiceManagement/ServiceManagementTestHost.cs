using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Notifications;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Application.Tables;
using Servora.Domain.Identity;
using Servora.Infrastructure.Modules;
using Servora.Infrastructure.Platform;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>
/// Builds the real service management DI graph (<see cref="ServiceManagementModule"/>) on top of an in-memory SQLite
/// database and test doubles for the kernel services.
/// </summary>
public sealed class ServiceManagementTestHost : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    /// <summary>Initializes a new instance of the <see cref="ServiceManagementTestHost"/> class.</summary>
    public ServiceManagementTestHost()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Database);
        services.AddSingleton<IAppDbContextFactory>(Database);
        services.AddSingleton<ICurrentUser>(User);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IEmailSender>(Email);
        services.AddSingleton<IAuditService>(Audit);
        services.AddSingleton<ITableRegistry, ReflectionTableRegistry>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddServiceManagementModule();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _scope = _provider.CreateScope();
    }

    /// <summary>Gets the database.</summary>
    public TestDatabase Database { get; } = new();

    /// <summary>Gets the switchable actor.</summary>
    public FakeCurrentUser User { get; } = new();

    /// <summary>Gets the clock.</summary>
    public TestClock Clock { get; } = new();

    /// <summary>Gets the recorded e-mails.</summary>
    public TestEmailSender Email { get; } = new();

    /// <summary>Gets the recorded audit entries.</summary>
    public TestAuditService Audit { get; } = new();

    /// <summary>Resolves a service from the test scope.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service.</returns>
    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Creates an active user.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="managerId">An optional manager.</param>
    /// <param name="active">Whether the user is active.</param>
    /// <returns>The user identifier.</returns>
    public async Task<Guid> AddUserAsync(string name, Guid? managerId = null, bool active = true)
    {
        var user = new User
        {
            UserName = name.Replace(" ", ".").ToLowerInvariant(),
            NormalizedUserName = name.Replace(" ", ".").ToUpperInvariant(),
            DisplayName = name,
            Email = name.Replace(" ", ".").ToLowerInvariant() + "@example.test",
            ManagerId = managerId,
            IsActive = active,
        };
        await using var db = Database.CreateDbContext();
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Creates a group with members.</summary>
    /// <param name="name">The group name.</param>
    /// <param name="type">The group type.</param>
    /// <param name="members">The member user identifiers.</param>
    /// <returns>The group identifier.</returns>
    public async Task<Guid> AddGroupAsync(string name, GroupType type, params Guid[] members)
    {
        var group = new Group { Name = name, Type = type };
        foreach (var member in members)
        {
            group.Members.Add(new GroupMember { GroupId = group.Id, UserId = member });
        }

        await using var db = Database.CreateDbContext();
        db.Set<Group>().Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        Database.Dispose();
    }
}
