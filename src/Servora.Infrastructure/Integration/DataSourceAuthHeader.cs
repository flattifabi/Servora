namespace Servora.Infrastructure.Integration;

/// <summary>An authentication header resolved from the secret store for one request (never logged or persisted).</summary>
/// <param name="Name">The header name.</param>
/// <param name="Value">The header value.</param>
internal sealed record DataSourceAuthHeader(string Name, string Value)
{
    /// <inheritdoc />
    public override string ToString() => $"{Name}: ***";
}
