using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>Builds the argument lists for <c>sc.exe</c> (each list element is passed as one argument).</summary>
internal static class ScCommandBuilder
{
    /// <summary>Recovery actions: restart after 1 min, 1 min, then 5 min; failure counter reset after one day.</summary>
    public const string RecoveryActions = "restart/60000/restart/60000/restart/300000";

    /// <summary>Gets the <c>sc.exe</c> account name for an account kind.</summary>
    /// <param name="account">The account kind.</param>
    /// <param name="serviceName">The service name (for the virtual account).</param>
    /// <returns>The account name.</returns>
    public static string AccountName(ServiceAccountKind account, string serviceName) => account switch
    {
        ServiceAccountKind.LocalSystem => "LocalSystem",
        ServiceAccountKind.NetworkService => @"NT AUTHORITY\NetworkService",
        _ => @"NT SERVICE\" + serviceName,
    };

    /// <summary>
    /// <c>sc create</c> with delayed automatic start. The executable path is wrapped in quotes so that a path with spaces
    /// cannot be misinterpreted (unquoted service path vulnerability).
    /// </summary>
    /// <param name="definition">The service definition.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> Create(ServiceDefinition definition) =>
    [
        "create", definition.Name,
        "binPath=", $"\"{definition.ExecutablePath}\"",
        "start=", "delayed-auto",
        "obj=", AccountName(definition.Account, definition.Name),
        "DisplayName=", definition.DisplayName,
    ];

    /// <summary><c>sc description</c>.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="description">The description.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> Description(string serviceName, string description) =>
        ["description", serviceName, description];

    /// <summary><c>sc failure</c> with restart recovery actions.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> Failure(string serviceName) =>
        ["failure", serviceName, "reset=", "86400", "actions=", RecoveryActions];

    /// <summary><c>sc failureflag</c>: also apply recovery actions when the service stops with a non-zero exit code.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> FailureFlag(string serviceName) => ["failureflag", serviceName, "1"];

    /// <summary><c>sc sidtype unrestricted</c>: gives the service its own SID (required for the virtual account and for ACLs).</summary>
    /// <param name="serviceName">The service name.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> SidType(string serviceName) => ["sidtype", serviceName, "unrestricted"];

    /// <summary><c>sc delete</c>.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> Delete(string serviceName) => ["delete", serviceName];
}
