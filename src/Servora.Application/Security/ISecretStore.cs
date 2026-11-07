namespace Servora.Application.Security;

/// <summary>
/// Encrypted storage for credentials the platform must reuse (LDAP bind password, SMTP password, data source
/// tokens, AI provider keys). Decrypted values are only handed to backend services, never to UI read models.
/// </summary>
public interface ISecretStore
{
    /// <summary>Creates or replaces a secret.</summary>
    /// <param name="name">The unique secret name, for example <c>ldap.bind_password</c>.</param>
    /// <param name="plainValue">The plain text value.</param>
    /// <param name="description">An optional description.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the secret is stored.</returns>
    Task SetAsync(string name, string plainValue, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Reads and decrypts a secret. For backend use only.</summary>
    /// <param name="name">The secret name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The plain value or <see langword="null"/> when the secret does not exist.</returns>
    Task<string?> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a secret exists without decrypting it.</summary>
    /// <param name="name">The secret name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the secret exists.</returns>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes a secret.</summary>
    /// <param name="name">The secret name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the secret is deleted.</returns>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}
