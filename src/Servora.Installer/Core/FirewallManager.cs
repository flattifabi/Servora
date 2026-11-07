using System.Globalization;
using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Manages the inbound Windows Firewall rule through <c>netsh advfirewall</c>. The rule only opens the configured TCP
/// port for the domain and private profiles; public networks stay closed.
/// </summary>
/// <param name="processes">The process runner.</param>
/// <param name="environment">The environment (location of <c>netsh.exe</c>).</param>
internal sealed class FirewallManager(IProcessRunner processes, IInstallerEnvironment environment)
{
    private string Netsh => Path.Combine(environment.SystemDirectory, "netsh.exe");

    /// <summary>Builds the arguments that add the rule.</summary>
    /// <param name="ruleName">The rule name.</param>
    /// <param name="port">The TCP port.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> AddRuleArguments(string ruleName, int port) =>
    [
        "advfirewall", "firewall", "add", "rule",
        $"name={ruleName}",
        "dir=in",
        "action=allow",
        "protocol=TCP",
        string.Create(CultureInfo.InvariantCulture, $"localport={port}"),
        "profile=domain,private",
        "enable=yes",
    ];

    /// <summary>Builds the arguments that delete the rule.</summary>
    /// <param name="ruleName">The rule name.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> DeleteRuleArguments(string ruleName) =>
        ["advfirewall", "firewall", "delete", "rule", $"name={ruleName}"];

    /// <summary>Creates (or replaces) the inbound rule for a port.</summary>
    /// <param name="ruleName">The rule name.</param>
    /// <param name="port">The TCP port.</param>
    /// <exception cref="InstallerException">netsh failed.</exception>
    public void AddRule(string ruleName, int port)
    {
        RemoveRule(ruleName);
        var result = processes.Run(Netsh, AddRuleArguments(ruleName, port));
        if (!result.Succeeded)
        {
            throw new InstallerException($"Could not create the firewall rule (netsh exit code {result.ExitCode}): {result.Output}");
        }
    }

    /// <summary>Removes the rule (ignores a missing rule).</summary>
    /// <param name="ruleName">The rule name.</param>
    /// <returns><see langword="true"/> when a rule was deleted.</returns>
    public bool RemoveRule(string ruleName) => processes.Run(Netsh, DeleteRuleArguments(ruleName)).Succeeded;
}
