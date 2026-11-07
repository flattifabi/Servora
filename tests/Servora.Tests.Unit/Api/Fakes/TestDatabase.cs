using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Infrastructure.Persistence;

namespace Servora.Tests.Unit.Api.Fakes;

/// <summary>In-memory SQLite database with the full Servora schema, usable as <see cref="IAppDbContextFactory"/>.</summary>
internal sealed class TestDatabase : IAppDbContextFactory, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ServoraDbContext> _options;

    private TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(_connection).Options;
    }

    /// <summary>Creates a database with the schema.</summary>
    /// <returns>The database.</returns>
    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        await using var db = database.Create();
        await db.Database.EnsureCreatedAsync();
        return database;
    }

    /// <summary>Creates a new context.</summary>
    /// <returns>The context.</returns>
    public ServoraDbContext Create() => new(_options);

    /// <inheritdoc />
    public IAppDbContext CreateDbContext() => Create();

    /// <inheritdoc />
    public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAppDbContext>(Create());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
