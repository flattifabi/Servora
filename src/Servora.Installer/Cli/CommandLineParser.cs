using System.Globalization;
using Servora.Installer.Model;

namespace Servora.Installer.Cli;

/// <summary>
/// Parses the <c>ServoraSetup.exe</c> command line. Syntax: <c>[command] [options]</c>; options accept
/// <c>--name value</c>, <c>--name=value</c> and the Windows style <c>/name</c> for switches.
/// </summary>
internal static class CommandLineParser
{
    /// <summary>Internal switch added when the installer relaunches itself through UAC.</summary>
    public const string ElevatedSwitch = "--elevated";

    private static readonly Dictionary<string, InstallerCommand> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["install"] = InstallerCommand.Install,
        ["update"] = InstallerCommand.Update,
        ["upgrade"] = InstallerCommand.Update,
        ["uninstall"] = InstallerCommand.Uninstall,
        ["remove"] = InstallerCommand.Uninstall,
        ["status"] = InstallerCommand.Status,
        ["help"] = InstallerCommand.Help,
        ["version"] = InstallerCommand.Version,
    };

    /// <summary>Parses the arguments.</summary>
    /// <param name="args">The raw arguments (without the executable).</param>
    /// <returns>The parse result.</returns>
    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        var options = new InstallerOptions();
        var errors = new List<string>();
        var commandSeen = false;

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (!IsOption(token))
            {
                if (commandSeen)
                {
                    errors.Add($"Unexpected argument '{token}'.");
                }
                else if (Commands.TryGetValue(token, out var command))
                {
                    options = options with { Command = command };
                    commandSeen = true;
                }
                else
                {
                    errors.Add($"Unknown command '{token}'. Valid commands: install, update, uninstall, status, help.");
                }

                continue;
            }

            var (name, inlineValue) = SplitOption(token);
            string? TakeValue()
            {
                if (inlineValue is not null)
                {
                    return inlineValue;
                }

                if (i + 1 < args.Count && !IsOption(args[i + 1]))
                {
                    i++;
                    return args[i];
                }

                errors.Add($"Option '--{name}' requires a value.");
                return null;
            }

            switch (name)
            {
                case "help" or "h" or "?":
                    options = options with { Command = InstallerCommand.Help };
                    break;
                case "version" or "v":
                    options = options with { Command = InstallerCommand.Version };
                    break;
                case "silent" or "s" or "quiet" or "q":
                    options = options with { Silent = true };
                    break;
                case "keep-data":
                    options = options with { KeepData = true };
                    break;
                case "firewall":
                    options = options with { Firewall = true };
                    break;
                case "no-firewall":
                    options = options with { Firewall = false };
                    break;
                case "no-browser":
                    options = options with { NoBrowser = true };
                    break;
                case "force":
                    options = options with { Force = true };
                    break;
                case "elevated":
                    options = options with { Elevated = true };
                    break;
                case "port" or "p":
                    if (TakeValue() is { } portText)
                    {
                        if (TryParseInt(portText, 1, 65535, out var port))
                        {
                            options = options with { Port = port };
                        }
                        else
                        {
                            errors.Add($"Invalid port '{portText}'. Use a number between 1 and 65535.");
                        }
                    }

                    break;
                case "install-dir":
                    if (TakeValue() is { } installDir)
                    {
                        options = options with { InstallDirectory = installDir };
                    }

                    break;
                case "data-dir":
                    if (TakeValue() is { } dataDir)
                    {
                        options = options with { DataDirectory = dataDir };
                    }

                    break;
                case "source":
                    if (TakeValue() is { } source)
                    {
                        options = options with { Source = source };
                    }

                    break;
                case "keep-backups":
                    if (TakeValue() is { } keepText)
                    {
                        if (TryParseInt(keepText, 1, 100, out var keep))
                        {
                            options = options with { KeepBackups = keep };
                        }
                        else
                        {
                            errors.Add($"Invalid backup count '{keepText}'. Use a number between 1 and 100.");
                        }
                    }

                    break;
                case "health-timeout":
                    if (TakeValue() is { } timeoutText)
                    {
                        if (TryParseInt(timeoutText, 5, 3600, out var seconds))
                        {
                            options = options with { HealthTimeout = TimeSpan.FromSeconds(seconds) };
                        }
                        else
                        {
                            errors.Add($"Invalid health timeout '{timeoutText}'. Use seconds between 5 and 3600.");
                        }
                    }

                    break;
                case "health-url":
                    if (TakeValue() is { } urlText)
                    {
                        if (Uri.TryCreate(urlText, UriKind.Absolute, out var url)
                            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
                        {
                            options = options with { HealthUrl = url };
                        }
                        else
                        {
                            errors.Add($"Invalid health URL '{urlText}'. Use an absolute http:// or https:// URL.");
                        }
                    }

                    break;
                case "service-account":
                    if (TakeValue() is { } accountText)
                    {
                        if (TryParseAccount(accountText, out var account))
                        {
                            options = options with { ServiceAccount = account };
                        }
                        else
                        {
                            errors.Add($"Invalid service account '{accountText}'. Use virtual, localsystem or networkservice.");
                        }
                    }

                    break;
                default:
                    errors.Add($"Unknown option '{token}'. Run 'ServoraSetup --help' for usage.");
                    break;
            }
        }

        return new CommandLineParseResult(options, errors);
    }

    /// <summary>Determines whether a command needs administrative rights.</summary>
    /// <param name="command">The command.</param>
    /// <returns><see langword="true"/> for commands that change the machine.</returns>
    public static bool RequiresElevation(InstallerCommand command) =>
        command is InstallerCommand.Auto or InstallerCommand.Install or InstallerCommand.Update or InstallerCommand.Uninstall;

    private static bool IsOption(string token) =>
        token.StartsWith('-') || (token.StartsWith('/') && token.Length > 1 && !token.Contains('\\') && token.IndexOf('/', 1) < 0);

    private static (string Name, string? Value) SplitOption(string token)
    {
        var body = token.TrimStart('-', '/');
        var separator = body.IndexOf('=');
        return separator < 0
            ? (body.ToLowerInvariant(), null)
            : (body[..separator].ToLowerInvariant(), body[(separator + 1)..]);
    }

    private static bool TryParseInt(string text, int min, int max, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;

    private static bool TryParseAccount(string text, out ServiceAccountKind account)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "virtual" or "nt service\\servora":
                account = ServiceAccountKind.Virtual;
                return true;
            case "localsystem" or "system":
                account = ServiceAccountKind.LocalSystem;
                return true;
            case "networkservice" or "nt authority\\networkservice":
                account = ServiceAccountKind.NetworkService;
                return true;
            default:
                account = ServiceAccountKind.Virtual;
                return false;
        }
    }
}
