using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Infrastructure.Persistence;

namespace Servora.Tests.Unit.Platform;

/// <summary>Creates <see cref="ServoraDbContext"/>s on a shared in-memory SQLite connection, wired to the scope's hooks and publisher.</summary>
/// <param name="connection">The open SQLite connection.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="actorContext">The actor context.</param>
/// <param name="services">The scoped service provider.</param>
public sealed class TestDbContextFactory(SqliteConnection connection, ICurrentUser currentUser, ActorContext actorContext, IServiceProvider services) : IAppDbContextFactory
{
    /// <inheritdoc />
    public IAppDbContext CreateDbContext() => Create();

    /// <inheritdoc />
    public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAppDbContext>(Create());

    /// <summary>Creates a context.</summary>
    /// <returns>The context.</returns>
    public ServoraDbContext Create() => new(new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(connection).Options)
    {
        ActorName = currentUser.DisplayName,
        ActorUserId = currentUser.UserId,
        EventDepth = actorContext.EventDepth,
        Services = services,
    };
}
