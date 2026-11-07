using Servora.Application.Common;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// Scoped <see cref="IAppDbContextFactory"/> that creates <see cref="ServoraDbContext"/> instances bound to the current
/// actor (for audit fields and record events) and to the scoped service provider (for saving hooks).
/// </summary>
/// <param name="optionsProvider">The options provider.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="actorContext">The actor context (event cascade depth).</param>
/// <param name="services">The scoped service provider.</param>
public sealed class AppDbContextFactory(
    DbContextOptionsProvider optionsProvider,
    ICurrentUser currentUser,
    ActorContext actorContext,
    IServiceProvider services) : IAppDbContextFactory
{
    /// <inheritdoc />
    public IAppDbContext CreateDbContext() => Create();

    /// <inheritdoc />
    public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAppDbContext>(Create());

    /// <summary>Creates a concrete context (for infrastructure code that needs the full EF API).</summary>
    /// <returns>The context.</returns>
    public ServoraDbContext Create() => new(optionsProvider.GetOptions())
    {
        ActorName = currentUser.DisplayName,
        ActorUserId = currentUser.UserId,
        EventDepth = actorContext.EventDepth,
        Services = services,
    };
}
