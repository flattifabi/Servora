using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Infrastructure.Persistence;

namespace Servora.Tests.Unit.Identity;

/// <summary>In-memory SQLite database exposing the platform context through <see cref="IAppDbContextFactory"/>.</summary>
public sealed class IdentityTestDatabase : IAppDbContextFactory, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ServoraDbContext> _options;

    /// <summary>Initializes a new instance of the <see cref="IdentityTestDatabase"/> class and creates the schema.</summary>
    public IdentityTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(_connection).Options;
        using var db = new ServoraDbContext(_options);
        db.Database.EnsureCreated();
    }

    /// <inheritdoc />
    public IAppDbContext CreateDbContext() => new ServoraDbContext(_options);

    /// <inheritdoc />
    public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();
}
