using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Servora.Application.Common;

/// <summary>
/// Provider-agnostic unit of work used by application services. Obtain instances from
/// <see cref="IAppDbContextFactory"/> and dispose them after each operation (Blazor Server circuits are long
/// lived, so contexts must never be held for the lifetime of a component).
/// </summary>
public interface IAppDbContext : IAsyncDisposable, IDisposable
{
    /// <summary>Gets the set for the given entity type.</summary>
    /// <typeparam name="TEntity">The entity type. It must be part of the model.</typeparam>
    /// <returns>The entity set.</returns>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    /// <summary>Gets the database facade (transactions, provider information).</summary>
    DatabaseFacade Database { get; }

    /// <summary>Persists all pending changes, raising record events and applying audit fields.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
