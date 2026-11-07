namespace Servora.Installer.Model;

/// <summary>Everything needed to register the Servora Windows service.</summary>
/// <param name="Name">The service name.</param>
/// <param name="DisplayName">The display name shown in the Services console.</param>
/// <param name="Description">The service description.</param>
/// <param name="ExecutablePath">The full path of the service executable (registered quoted).</param>
/// <param name="Account">The account kind the service runs under.</param>
/// <param name="Environment">Service-specific environment variables (for example <c>SERVORA_DATA_DIR</c>); may be empty.</param>
internal sealed record ServiceDefinition(
    string Name,
    string DisplayName,
    string Description,
    string ExecutablePath,
    ServiceAccountKind Account,
    IReadOnlyDictionary<string, string> Environment);
