using System.Globalization;
using System.Text;
using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Platform;

/// <summary>
/// Coloured console implementation of <see cref="IInstallerUi"/>. Colours are disabled when output is redirected or
/// the <c>NO_COLOR</c> environment variable is set. Optionally mirrors every line into a transcript file.
/// </summary>
internal sealed class ConsoleUi : IInstallerUi
{
    private const int LabelWidth = 20;
    private readonly bool _useColor;
    private readonly object _sync = new();
    private string? _transcriptPath;

    /// <summary>Initializes a new instance of the <see cref="ConsoleUi"/> class.</summary>
    /// <param name="silent">Whether prompts are suppressed.</param>
    public ConsoleUi(bool silent)
    {
        IsInteractive = !silent && !Console.IsInputRedirected;
        _useColor = !Console.IsOutputRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    /// <inheritdoc />
    public bool IsInteractive { get; }

    /// <inheritdoc />
    public void Banner(string version)
    {
        string[] logo =
        [
            @"   ____                                 ",
            @"  / ___|  ___ _ ____   _____  _ __ __ _ ",
            @"  \___ \ / _ \ '__\ \ / / _ \| '__/ _` |",
            @"   ___) |  __/ |   \ V / (_) | | | (_| |",
            @"  |____/ \___|_|    \_/ \___/|_|  \__,_|",
        ];
        WriteLine();
        foreach (var line in logo)
        {
            WriteLine((line, ConsoleColor.Cyan));
        }

        WriteLine(("  Setup ", ConsoleColor.White), (version, ConsoleColor.Cyan), ("  -  install, update and manage the Servora Windows service", ConsoleColor.DarkGray));
    }

    /// <inheritdoc />
    public void Heading(string text)
    {
        WriteLine();
        WriteLine(("  " + text, ConsoleColor.White));
        WriteLine(("  " + new string('-', Math.Max(text.Length, 3)), ConsoleColor.DarkCyan));
    }

    /// <inheritdoc />
    public void Info(string text)
    {
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            WriteLine(("  " + line, null));
        }
    }

    /// <inheritdoc />
    public void Detail(string label, string value, MessageTone tone = MessageTone.Normal) =>
        WriteLine(("  " + label.PadRight(LabelWidth, ' '), ConsoleColor.DarkGray), (value, Color(tone)));

    /// <inheritdoc />
    public void Step(string text) => WriteLine(("  > ", ConsoleColor.Cyan), (text, null));

    /// <inheritdoc />
    public void Success(string text) => WriteLine(("    [ OK ] ", ConsoleColor.Green), (text, null));

    /// <inheritdoc />
    public void Warning(string text) => WriteLine(("    [WARN] ", ConsoleColor.Yellow), (text, ConsoleColor.Yellow));

    /// <inheritdoc />
    public void Error(string text) => WriteLine(("    [FAIL] ", ConsoleColor.Red), (text, ConsoleColor.Red));

    /// <inheritdoc />
    public string Ask(string question, string defaultValue)
    {
        if (!IsInteractive)
        {
            return defaultValue;
        }

        Write(("  ? ", ConsoleColor.Cyan), (question, null), ($" [{defaultValue}]", ConsoleColor.DarkGray), (": ", null));
        var answer = Console.ReadLine();
        Transcript(answer ?? string.Empty);
        return string.IsNullOrWhiteSpace(answer) ? defaultValue : answer.Trim();
    }

    /// <inheritdoc />
    public bool Confirm(string question, bool defaultValue)
    {
        if (!IsInteractive)
        {
            return defaultValue;
        }

        while (true)
        {
            Write(("  ? ", ConsoleColor.Cyan), (question, null), (defaultValue ? " [Y/n]" : " [y/N]", ConsoleColor.DarkGray), (": ", null));
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            Transcript(answer ?? string.Empty);
            switch (answer)
            {
                case null or "":
                    return defaultValue;
                case "y" or "yes" or "j" or "ja":
                    return true;
                case "n" or "no" or "nein":
                    return false;
            }
        }
    }

    /// <inheritdoc />
    public int Choose(string title, IReadOnlyList<string> options)
    {
        if (!IsInteractive)
        {
            return -1;
        }

        WriteLine();
        WriteLine(("  " + title, ConsoleColor.White));
        for (var i = 0; i < options.Count; i++)
        {
            WriteLine(($"    {i + 1}) ", ConsoleColor.Cyan), (options[i], null));
        }

        WriteLine(("    0) ", ConsoleColor.Cyan), ("Exit", ConsoleColor.DarkGray));
        while (true)
        {
            Write(("  ? ", ConsoleColor.Cyan), ("Select", null), ($" [0-{options.Count}]", ConsoleColor.DarkGray), (": ", null));
            var answer = Console.ReadLine();
            Transcript(answer ?? string.Empty);
            if (answer is null)
            {
                return -1;
            }

            if (int.TryParse(answer.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 0 && number <= options.Count)
            {
                return number - 1;
            }
        }
    }

    /// <inheritdoc />
    public void Pause(string message)
    {
        if (!IsInteractive)
        {
            return;
        }

        WriteLine();
        Write(("  " + message, ConsoleColor.DarkGray));
        Console.ReadLine();
        Console.WriteLine();
    }

    /// <inheritdoc />
    public void StartTranscript(string logFilePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            File.AppendAllText(logFilePath, $"# ServoraSetup transcript {DateTimeOffset.Now:O}{Environment.NewLine}", Encoding.UTF8);
            _transcriptPath = logFilePath;
            WriteLine(("  Log file: " + logFilePath, ConsoleColor.DarkGray));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _transcriptPath = null;
        }
    }

    private static ConsoleColor? Color(MessageTone tone) => tone switch
    {
        MessageTone.Good => ConsoleColor.Green,
        MessageTone.Warning => ConsoleColor.Yellow,
        MessageTone.Bad => ConsoleColor.Red,
        MessageTone.Muted => ConsoleColor.DarkGray,
        MessageTone.Accent => ConsoleColor.Cyan,
        _ => null,
    };

    private void WriteLine(params (string Text, ConsoleColor? Color)[] segments)
    {
        Write(segments);
        lock (_sync)
        {
            Console.WriteLine();
        }
    }

    private void Write(params (string Text, ConsoleColor? Color)[] segments)
    {
        lock (_sync)
        {
            foreach (var (text, color) in segments)
            {
                if (_useColor && color is { } c)
                {
                    Console.ForegroundColor = c;
                    Console.Write(text);
                    Console.ResetColor();
                }
                else
                {
                    Console.Write(text);
                }
            }
        }

        Transcript(string.Concat(segments.Select(s => s.Text)));
    }

    private void Transcript(string text)
    {
        if (_transcriptPath is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(_transcriptPath, text + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The log folder may have been deleted (uninstall); stop mirroring instead of failing.
            _transcriptPath = null;
        }
    }
}
