namespace Servora.Application.Api;

/// <summary>Effective state of an API key.</summary>
public enum ApiKeyStatus
{
    /// <summary>The key can be used.</summary>
    Active = 0,

    /// <summary>The key passed its expiry time.</summary>
    Expired = 1,

    /// <summary>The key was revoked.</summary>
    Revoked = 2,
}
