namespace Servora.Infrastructure.Integration;

/// <summary>Raised when an outbound data source request is blocked (SSRF protection, insecure or excessive redirects).</summary>
/// <param name="message">The user-safe reason.</param>
public sealed class DataSourceBlockedException(string message) : Exception(message);
