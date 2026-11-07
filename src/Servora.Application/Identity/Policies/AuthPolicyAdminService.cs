using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;

namespace Servora.Application.Identity.Policies;

/// <summary>Default <see cref="IAuthPolicyAdminService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="provider">The policy provider.</param>
/// <param name="store">The identity settings store.</param>
/// <param name="audit">The audit service.</param>
public sealed class AuthPolicyAdminService(
    ICurrentUser currentUser,
    IAuthPolicyProvider provider,
    IdentitySettingsStore store,
    IAuditService audit) : IAuthPolicyAdminService
{
    /// <inheritdoc />
    public async Task<Result<AuthPolicySettings>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageAuthPolicies, cancellationToken))
        {
            return Result<AuthPolicySettings>.Forbidden();
        }

        return Result<AuthPolicySettings>.Success(await provider.GetAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result> SaveAsync(AuthPolicySettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageAuthPolicies, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (settings.Validate() is { } error)
        {
            return Result.Invalid(error);
        }

        var before = await provider.GetAsync(cancellationToken);
        await store.SaveAsync(AuthPolicySettings.SettingKey, settings with { }, "Authentication policy (password rules, lockout, sessions, local sign-in).", cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "auth_policy.updated")
        {
            TargetType = "sys_properties",
            TargetName = AuthPolicySettings.SettingKey,
            Details = new { before, after = settings },
        }, cancellationToken);
        return Result.Success();
    }
}
