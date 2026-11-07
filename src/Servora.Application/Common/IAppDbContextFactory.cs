namespace Servora.Application.Common;

/// <summary>
/// Creates short-lived <see cref="IAppDbContext"/> instances bound to the current actor. Register as scoped.
/// </summary>
public interface IAppDbContextFactory
{
    /// <summary>Creates a new database context. The caller owns and must dispose it.</summary>
    /// <returns>A new context.</returns>
    IAppDbContext CreateDbContext();

    /// <summary>Creates a new database context asynchronously. The caller owns and must dispose it.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A new context.</returns>
    Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}
