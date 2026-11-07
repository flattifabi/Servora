using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Captures output and answers prompts from a script (defaults when the script is empty).</summary>
internal sealed class RecordingUi : IInstallerUi
{
    /// <summary>Gets or sets a value indicating whether prompts are answered from <see cref="Answers"/>.</summary>
    public bool IsInteractive { get; set; }

    /// <summary>Gets the scripted answers (text for Ask, "y"/"n" for Confirm, number for Choose).</summary>
    public Queue<string> Answers { get; } = new();

    /// <summary>Gets all output lines with a level prefix.</summary>
    public List<string> Lines { get; } = [];

    /// <summary>Gets the error lines.</summary>
    public IEnumerable<string> Errors => Lines.Where(l => l.StartsWith("ERROR ", StringComparison.Ordinal));

    /// <summary>Gets the transcript path, when started.</summary>
    public string? TranscriptPath { get; private set; }

    /// <inheritdoc />
    public void Banner(string version) => Lines.Add("BANNER " + version);

    /// <inheritdoc />
    public void Heading(string text) => Lines.Add("HEADING " + text);

    /// <inheritdoc />
    public void Info(string text) => Lines.Add("INFO " + text);

    /// <inheritdoc />
    public void Detail(string label, string value, MessageTone tone = MessageTone.Normal) => Lines.Add($"DETAIL {label}: {value}");

    /// <inheritdoc />
    public void Step(string text) => Lines.Add("STEP " + text);

    /// <inheritdoc />
    public void Success(string text) => Lines.Add("OK " + text);

    /// <inheritdoc />
    public void Warning(string text) => Lines.Add("WARN " + text);

    /// <inheritdoc />
    public void Error(string text) => Lines.Add("ERROR " + text);

    /// <inheritdoc />
    public string Ask(string question, string defaultValue)
    {
        var answer = IsInteractive && Answers.Count > 0 ? Answers.Dequeue() : string.Empty;
        return string.IsNullOrEmpty(answer) ? defaultValue : answer;
    }

    /// <inheritdoc />
    public bool Confirm(string question, bool defaultValue)
    {
        var answer = IsInteractive && Answers.Count > 0 ? Answers.Dequeue() : string.Empty;
        return answer switch
        {
            "y" => true,
            "n" => false,
            _ => defaultValue,
        };
    }

    /// <inheritdoc />
    public int Choose(string title, IReadOnlyList<string> options)
    {
        var answer = IsInteractive && Answers.Count > 0 ? Answers.Dequeue() : "0";
        return int.Parse(answer, System.Globalization.CultureInfo.InvariantCulture) - 1;
    }

    /// <inheritdoc />
    public void Pause(string message) => Lines.Add("PAUSE");

    /// <inheritdoc />
    public void StartTranscript(string logFilePath) => TranscriptPath = logFilePath;
}
