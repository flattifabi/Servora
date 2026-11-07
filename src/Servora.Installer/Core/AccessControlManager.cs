using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Hardens the access control lists of the install and data directories with <c>icacls.exe</c>. Well-known SIDs are
/// used instead of names so that localized Windows editions work.
/// <list type="bullet">
/// <item>Install directory: SYSTEM and Administrators full control, Users read &amp; execute (nobody else can replace binaries).</item>
/// <item>Data directory: SYSTEM and Administrators full control, the service account modify; no access for other
/// users (database, Data Protection keys, attachments and logs are private to the service).</item>
/// </list>
/// </summary>
/// <param name="processes">The process runner.</param>
/// <param name="environment">The environment (location of <c>icacls.exe</c>).</param>
internal sealed class AccessControlManager(IProcessRunner processes, IInstallerEnvironment environment)
{
    private const string SystemSid = "*S-1-5-18";
    private const string AdministratorsSid = "*S-1-5-32-544";
    private const string UsersSid = "*S-1-5-32-545";
    private const string NetworkServiceSid = "*S-1-5-20";

    private string Icacls => Path.Combine(environment.SystemDirectory, "icacls.exe");

    /// <summary>Builds the icacls arguments for the install directory.</summary>
    /// <param name="installDirectory">The install directory.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> InstallDirectoryArguments(string installDirectory) =>
    [
        installDirectory,
        "/inheritance:r",
        "/grant:r",
        SystemSid + ":(OI)(CI)F",
        AdministratorsSid + ":(OI)(CI)F",
        UsersSid + ":(OI)(CI)RX",
        "/Q",
    ];

    /// <summary>Builds the icacls arguments for the data directory.</summary>
    /// <param name="dataDirectory">The data directory.</param>
    /// <param name="account">The service account kind.</param>
    /// <param name="serviceName">The service name (virtual account).</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> DataDirectoryArguments(string dataDirectory, ServiceAccountKind account, string serviceName)
    {
        var arguments = new List<string>
        {
            dataDirectory,
            "/inheritance:r",
            "/grant:r",
            SystemSid + ":(OI)(CI)F",
            AdministratorsSid + ":(OI)(CI)F",
        };

        switch (account)
        {
            case ServiceAccountKind.Virtual:
                arguments.Add(ScCommandBuilder.AccountName(account, serviceName) + ":(OI)(CI)M");
                break;
            case ServiceAccountKind.NetworkService:
                arguments.Add(NetworkServiceSid + ":(OI)(CI)M");
                break;
        }

        arguments.Add("/Q");
        return arguments;
    }

    /// <summary>Applies the install directory ACL.</summary>
    /// <param name="installDirectory">The install directory.</param>
    /// <exception cref="InstallerException">icacls failed.</exception>
    public void SecureInstallDirectory(string installDirectory) =>
        Run(InstallDirectoryArguments(installDirectory), installDirectory);

    /// <summary>Applies the data directory ACL (the service must already exist for the virtual account).</summary>
    /// <param name="dataDirectory">The data directory.</param>
    /// <param name="account">The service account kind.</param>
    /// <param name="serviceName">The service name.</param>
    /// <exception cref="InstallerException">icacls failed.</exception>
    public void SecureDataDirectory(string dataDirectory, ServiceAccountKind account, string serviceName) =>
        Run(DataDirectoryArguments(dataDirectory, account, serviceName), dataDirectory);

    private void Run(IReadOnlyList<string> arguments, string path)
    {
        var result = processes.Run(Icacls, arguments);
        if (!result.Succeeded)
        {
            throw new InstallerException($"Could not set permissions on '{path}' (icacls exit code {result.ExitCode}): {result.Output}");
        }
    }
}
