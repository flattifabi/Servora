using Servora.Installer.Model;

namespace Servora.Installer.Abstractions;

/// <summary>Operator interaction: coloured output and prompts. Silent mode answers prompts with their defaults.</summary>
internal interface IInstallerUi
{
    /// <summary>Gets a value indicating whether prompts are shown (not silent and input is not redirected).</summary>
    bool IsInteractive { get; }

    /// <summary>Prints the product banner.</summary>
    /// <param name="version">The installer version.</param>
    void Banner(string version);

    /// <summary>Prints a section heading.</summary>
    /// <param name="text">The heading.</param>
    void Heading(string text);

    /// <summary>Prints an informational line.</summary>
    /// <param name="text">The text.</param>
    void Info(string text);

    /// <summary>Prints an aligned label/value line.</summary>
    /// <param name="label">The label.</param>
    /// <param name="value">The value.</param>
    /// <param name="tone">The value colour.</param>
    void Detail(string label, string value, MessageTone tone = MessageTone.Normal);

    /// <summary>Prints the start of a workflow step.</summary>
    /// <param name="text">The step description.</param>
    void Step(string text);

    /// <summary>Prints a success line.</summary>
    /// <param name="text">The text.</param>
    void Success(string text);

    /// <summary>Prints a warning line.</summary>
    /// <param name="text">The text.</param>
    void Warning(string text);

    /// <summary>Prints an error line.</summary>
    /// <param name="text">The text.</param>
    void Error(string text);

    /// <summary>Asks for a text value.</summary>
    /// <param name="question">The question.</param>
    /// <param name="defaultValue">The value used on empty input and in silent mode.</param>
    /// <returns>The answer.</returns>
    string Ask(string question, string defaultValue);

    /// <summary>Asks a yes/no question.</summary>
    /// <param name="question">The question.</param>
    /// <param name="defaultValue">The answer used on empty input and in silent mode.</param>
    /// <returns>The answer.</returns>
    bool Confirm(string question, bool defaultValue);

    /// <summary>Shows a numbered menu.</summary>
    /// <param name="title">The menu title.</param>
    /// <param name="options">The options (numbered from 1).</param>
    /// <returns>The zero-based index of the chosen option, or -1 for "exit".</returns>
    int Choose(string title, IReadOnlyList<string> options);

    /// <summary>Waits for a key press (interactive mode only).</summary>
    /// <param name="message">The prompt text.</param>
    void Pause(string message);

    /// <summary>Copies every following line into a log file (best effort).</summary>
    /// <param name="logFilePath">The log file path.</param>
    void StartTranscript(string logFilePath);
}
