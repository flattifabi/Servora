using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Identity.Ldap;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Identity;

/// <summary>
/// Runs directory provisioning in a fresh DI scope as a named system actor. Used during sign-in, where the request has
/// no authenticated principal yet, so audit fields and record events show "Directory sign-in" instead of "Anonymous"
/// – without elevating the caller's own scope.
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
public sealed class SystemScopeProvisioner(IServiceScopeFactory scopeFactory)
{
    /// <summary>Provisions a directory entry as the system actor.</summary>
    /// <param name="entry">The directory entry.</param>
    /// <param name="settings">The directory settings.</param>
    /// <param name="mappings">The active group mappings.</param>
    /// <param name="allowCreate">Whether an unknown user may be created.</param>
    /// <param name="actorName">The system actor display name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provisioning result.</returns>
    public async Task<DirectoryProvisioningResult> ProvisionAsync(
        DirectoryUserEntry entry,
        DirectorySettings settings,
        IReadOnlyList<DirectoryGroupMapping> mappings,
        bool allowCreate,
        string actorName,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed(actorName));
        var provisioning = scope.ServiceProvider.GetRequiredService<IDirectoryProvisioningService>();
        return await provisioning.ProvisionAsync(entry, settings, mappings, allowCreate, cancellationToken);
    }
}
