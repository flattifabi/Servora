namespace Servora.Application.Api;

/// <summary>Result of creating or rotating a key. The token is returned exactly once and is never stored.</summary>
/// <param name="Id">The key identifier.</param>
/// <param name="Name">The key name.</param>
/// <param name="Token">The plain token.</param>
public sealed record ApiKeySecretResult(Guid Id, string Name, string Token)
{
    /// <summary>Returns a redacted representation so the token never ends up in logs by accident.</summary>
    /// <returns>The redacted string.</returns>
    public override string ToString() => $"ApiKeySecretResult {{ Id = {Id}, Name = {Name}, Token = *** }}";
}
