using System.Reflection;

namespace Servora.Application.Mcp;

/// <summary>Protocol constants and version negotiation of the Servora MCP server (Streamable HTTP, JSON-RPC 2.0).</summary>
public static class McpProtocol
{
    /// <summary>The server name reported in <c>initialize</c>.</summary>
    public const string ServerName = "servora";

    /// <summary>The newest supported protocol revision (returned when the client asks for an unknown revision).</summary>
    public const string LatestVersion = "2025-11-25";

    /// <summary>The HTTP header carrying the negotiated protocol version on subsequent requests.</summary>
    public const string VersionHeader = "MCP-Protocol-Version";

    /// <summary>Gets the supported protocol revisions, newest first.</summary>
    public static IReadOnlyList<string> SupportedVersions { get; } = [LatestVersion, "2025-06-18", "2025-03-26", "2024-11-05"];

    /// <summary>Gets the server version (assembly version).</summary>
    public static string ServerVersion { get; } =
        typeof(McpProtocol).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(McpProtocol).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>
    /// Negotiates the protocol version: if the client's requested revision is supported it is echoed, otherwise the
    /// latest supported revision is returned (the client then decides whether it can continue).
    /// </summary>
    /// <param name="requested">The revision requested by the client.</param>
    /// <returns>The revision to use.</returns>
    public static string Negotiate(string? requested) =>
        requested is not null && SupportedVersions.Contains(requested, StringComparer.Ordinal) ? requested : LatestVersion;

    /// <summary>Checks whether a revision is supported.</summary>
    /// <param name="version">The revision.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    public static bool IsSupported(string? version) => version is not null && SupportedVersions.Contains(version, StringComparer.Ordinal);
}
