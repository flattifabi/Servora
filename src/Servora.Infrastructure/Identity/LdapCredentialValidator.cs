using Microsoft.Extensions.Logging;
using Servora.Application.Identity;
using Servora.Application.Identity.Ldap;
using Servora.Application.Observability;
using Servora.Domain.Identity;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Identity;

/// <summary>
/// <see cref="IExternalCredentialValidator"/> for LDAP / Active Directory. Verifies the password with a user bind,
/// provisions unknown users just in time (when enabled), refreshes profile attributes and reconciles mapped roles and
/// groups on every successful sign-in.
/// </summary>
/// <param name="configuration">The directory configuration provider.</param>
/// <param name="client">The directory client.</param>
/// <param name="provisioner">Runs provisioning as a system actor.</param>
/// <param name="audit">The audit service.</param>
/// <param name="logger">The logger.</param>
public sealed class LdapCredentialValidator(
    IDirectoryConfigurationProvider configuration,
    IDirectoryClient client,
    SystemScopeProvisioner provisioner,
    IAuditService audit,
    ILogger<LdapCredentialValidator> logger) : IExternalCredentialValidator
{
    /// <summary>The authentication source key.</summary>
    public const string SourceKey = "directory";

    /// <inheritdoc />
    public string Source => SourceKey;

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var settings = await configuration.GetSettingsAsync(cancellationToken);
        return settings.Enabled && settings.IsConnectionConfigured;
    }

    /// <inheritdoc />
    public async Task<(SignInOutcome Outcome, User? User)> ValidateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (SignInOutcome.InvalidCredentials, null);
        }

        var settings = await configuration.GetSettingsAsync(cancellationToken);
        var login = DirectoryLoginName.Parse(userName);
        if (login is null || !login.MatchesDomain(settings.NetBiosDomain))
        {
            return (SignInOutcome.InvalidCredentials, null);
        }

        var bindPassword = await configuration.GetBindPasswordAsync(cancellationToken);
        var result = await client.AuthenticateAsync(settings, bindPassword, login, password, cancellationToken);
        switch (result.Outcome)
        {
            case DirectoryLookupOutcome.Success:
                break;
            case DirectoryLookupOutcome.Unavailable:
            case DirectoryLookupOutcome.ConfigurationError:
                logger.LogWarning("LDAP sign-in could not reach the directory: {Message}", result.Message);
                return (SignInOutcome.DirectoryUnavailable, null);
            default:
                return (SignInOutcome.InvalidCredentials, null);
        }

        var entry = result.Entry!;
        if (entry.IsDisabled)
        {
            return (SignInOutcome.Disabled, null);
        }

        var mappings = await configuration.GetActiveMappingsAsync(cancellationToken);
        var provisioned = await provisioner.ProvisionAsync(entry, settings, mappings, settings.JitProvisioning, "Directory sign-in", cancellationToken);
        if (!provisioned.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry("auth", "directory.login_refused", AuditOutcome.Denied)
            {
                ActorNameOverride = entry.UserName,
                Message = provisioned.Error ?? (provisioned.Skipped ? "The directory user has no Servora account and just-in-time provisioning is disabled." : "Provisioning failed."),
            }, cancellationToken);
            return (SignInOutcome.InvalidCredentials, null);
        }

        return (SignInOutcome.Success, provisioned.User);
    }
}
