using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Infrastructure.Persistence;

namespace Servora.Tests.Unit.Observability;

/// <summary>In-memory SQLite database with the full Servora model, usable as <see cref="IAppDbContextFactory"/>.</summary>
internal sealed class SqliteTestDatabase : IAppDbContextFactory, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ServoraDbContext> _options;

    /// <summary>Initializes a new instance of the <see cref="SqliteTestDatabase"/> class and creates the schema.</summary>
    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(_connection).Options;
        using var db = new ServoraDbContext(_options);
        db.Database.EnsureCreated();
    }

    /// <summary>Creates a concrete context.</summary>
    /// <returns>The context.</returns>
    public ServoraDbContext Create() => new(_options);

    /// <inheritdoc />
    public IAppDbContext CreateDbContext() => Create();

    /// <inheritdoc />
    public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAppDbContext>(Create());

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();
}
