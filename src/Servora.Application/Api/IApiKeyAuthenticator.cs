using System.Net;

namespace Servora.Application.Api;

/// <summary>Validates presented API key tokens (used by the <c>ApiKey</c> authentication scheme).</summary>
public interface IApiKeyAuthenticator
{
    /// <summary>Authenticates a token: format, constant-time secret check, revocation, expiry, owner state and IP ranges.</summary>
    /// <param name="token">The presented token.</param>
    /// <param name="remoteAddress">The client address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<ApiKeyAuthenticationResult> AuthenticateAsync(string token, IPAddress? remoteAddress, CancellationToken cancellationToken = default);

    /// <summary>Drops cached data of a key after it was changed, rotated or revoked.</summary>
    /// <param name="keyId">The public key id.</param>
    void Invalidate(string keyId);
}
