using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Servora.Application.Security;
using Servora.Infrastructure.Security;

namespace Servora.Web.Security;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/>s against the principal in the authorization context. Users are
/// resolved through <see cref="IPermissionService"/> (roles and group roles); API key and agent principals must carry
/// the permission as scope claim and their owning user must hold it as well.
/// </summary>
/// <param name="permissionService">The permission service.</param>
public sealed class PermissionAuthorizationHandler(IPermissionService permissionService) : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var hasUser = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        var actorType = principal.FindFirstValue(ServoraClaimTypes.ActorType);
        if (actorType is "apikey" or "agent")
        {
            var scopes = principal.FindAll(ServoraClaimTypes.Permission).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
            if (!PermissionMatcher.IsGranted(scopes, requirement.Permission))
            {
                return;
            }

            if (!hasUser || await permissionService.UserHasPermissionAsync(userId, requirement.Permission))
            {
                context.Succeed(requirement);
            }

            return;
        }

        if (hasUser && await permissionService.UserHasPermissionAsync(userId, requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
