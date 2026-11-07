using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servora.Application.Activity;
using Servora.Application.Automation;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Events;
using Servora.Application.Notifications;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Infrastructure.Modules;
using Servora.Infrastructure.Placeholders;

namespace Servora.Tests.Unit.Platform;

/// <summary>
/// Real Platform module services on an in-memory SQLite database with test doubles for the actor, e-mail, audit and
/// event publishing. Record events are captured and can be dispatched to the registered handlers on demand.
/// </summary>
public sealed class PlatformTestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private PlatformTestHost(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    /// <summary>Gets the shared identity (user id and permissions of the acting user).</summary>
    public TestIdentity Identity => _provider.GetRequiredService<TestIdentity>();

    /// <summary>Gets the captured record events.</summary>
    public TestEventPublisher Events => _provider.GetRequiredService<TestEventPublisher>();

    /// <summary>Gets the captured e-mails.</summary>
    public TestEmailSender Emails => _provider.GetRequiredService<TestEmailSender>();

    /// <summary>Gets the controllable clock.</summary>
    public TestClock Clock => _provider.GetRequiredService<TestClock>();

    /// <summary>Creates and initializes a host.</summary>
    /// <returns>The host.</returns>
    public static async Task<PlatformTestHost> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(connection);
        services.AddSingleton<TestIdentity>();
        services.AddSingleton<TestClock>();
        services.AddSingleton<IClock>(sp => sp.GetRequiredService<TestClock>());
        services.AddSingleton<TestEventPublisher>();
        services.AddSingleton<IRecordEventPublisher>(sp => sp.GetRequiredService<TestEventPublisher>());
        services.AddSingleton<TestEmailSender>();
        services.AddScoped<ActorContext>();
        services.AddScoped<ICurrentUser, TestCurrentUser>();
        services.AddScoped<IAppDbContextFactory, TestDbContextFactory>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddSingleton<IAuditService, TestAuditService>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IDirectoryLookupService, DirectoryLookupService>();
        services.AddScoped<IFlowExecutionService, UnavailableFlowExecutionService>();
        services.AddPlatformModule();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<TestEmailSender>());

        var provider = services.BuildServiceProvider();
        var host = new PlatformTestHost(connection, provider);
        await host.RunAsync<IAppDbContextFactory>(async factory =>
        {
            await using var db = (Servora.Infrastructure.Persistence.ServoraDbContext)await factory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();
        });
        return host;
    }

    /// <summary>Runs code with a service resolved from a new scope.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="action">The code.</param>
    /// <returns>A task that completes when the code is done.</returns>
    public async Task RunAsync<T>(Func<T, Task> action)
        where T : notnull
    {
        using var scope = _provider.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<T>());
    }

    /// <summary>Runs code with a service resolved from a new scope and returns its result.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="action">The code.</param>
    /// <returns>The result.</returns>
    public async Task<TResult> RunAsync<T, TResult>(Func<T, Task<TResult>> action)
        where T : notnull
    {
        using var scope = _provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<T>());
    }

    /// <summary>Runs code with a database context of a new scope.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="action">The code.</param>
    /// <returns>The result.</returns>
    public async Task<TResult> DbAsync<TResult>(Func<IAppDbContext, Task<TResult>> action)
    {
        using var scope = _provider.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IAppDbContextFactory>().CreateDbContextAsync();
        return await action(db);
    }

    /// <summary>Dispatches all captured events to the registered record event handlers (like the kernel processor, as system actor).</summary>
    /// <returns>The number of dispatched events.</returns>
    public async Task<int> DispatchEventsAsync()
    {
        var count = 0;
        while (Events.Events.TryDequeue(out var recordEvent))
        {
            count++;
            using var scope = _provider.CreateScope();
            var actor = scope.ServiceProvider.GetRequiredService<ActorContext>();
            actor.Set(ActorInfo.System);
            actor.EventDepth = recordEvent.Depth + 1;
            foreach (var handler in scope.ServiceProvider.GetServices<IRecordEventHandler>().OrderBy(h => h.Order))
            {
                await handler.HandleAsync(recordEvent, CancellationToken.None);
            }
        }

        return count;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
