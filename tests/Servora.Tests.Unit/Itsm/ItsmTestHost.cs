using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Events;
using Servora.Application.Itsm;
using Servora.Application.Platform;
using Servora.Domain.Identity;
using Servora.Infrastructure.Persistence;
using Servora.Infrastructure.Placeholders;

namespace Servora.Tests.Unit.Itsm;

/// <summary>
/// In-memory SQLite host for ITSM service tests: a real <see cref="ServoraDbContext"/> (with saving hooks), the real
/// application services and fakes for the current user and the clock.
/// </summary>
internal sealed class ItsmTestHost : IAppDbContextFactory, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;
    private readonly DbContextOptions<ServoraDbContext> _options;

    private ItsmTestHost()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(_connection).Options;
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(Clock);
        services.AddScoped<IRecordSavingHook, ItsmSavingHook>();
        _services = services.BuildServiceProvider();

        var lookup = new DirectoryLookupService(this);
        Incidents = new IncidentService(this, User, new NumberSequenceService(this), lookup, new EmptySlaService(), Clock);
        Problems = new ProblemService(this, User, new NumberSequenceService(this), lookup, Clock);
        MyWork = new MyWorkService(this, User, lookup, Clock, new UnavailableApprovalService());
    }

    /// <summary>Gets the current user fake.</summary>
    public FakeCurrentUser User { get; } = new();

    /// <summary>Gets the clock fake.</summary>
    public FixedClock Clock { get; } = new();

    /// <summary>Gets the incident service under test.</summary>
    public IncidentService Incidents { get; }

    /// <summary>Gets the problem service under test.</summary>
    public ProblemService Problems { get; }

    /// <summary>Gets the My Work service under test.</summary>
    public MyWorkService MyWork { get; }

    /// <summary>Creates a host with the schema and the current user as a persisted user.</summary>
    /// <returns>The host.</returns>
    public static async Task<ItsmTestHost> CreateAsync()
    {
        var host = new ItsmTestHost();
        await using var db = host.Create();
        await db.Database.EnsureCreatedAsync();
        db.Set<User>().Add(new User { Id = host.User.UserId!.Value, UserName = "tester", NormalizedUserName = "TESTER", DisplayName = "Test User" });
        await db.SaveChangesAsync();
        return host;
    }

    /// <summary>Creates a context bound to the hooks and the current user.</summary>
    /// <returns>The context.</returns>
    public ServoraDbContext Create() => new(_options)
    {
        Services = _services,
        ActorName = User.DisplayName,
        ActorUserId = User.UserId,
    };

    /// <inheritdoc />
    public IAppDbContext CreateDbContext() => Create();

    /// <inheritdoc />
    public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAppDbContext>(Create());

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
