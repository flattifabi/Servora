using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Records external tool invocations; every call succeeds unless <see cref="Handler"/> says otherwise.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    /// <summary>Gets the recorded runs (file name + arguments).</summary>
    public List<(string FileName, IReadOnlyList<string> Arguments)> Runs { get; } = [];

    /// <summary>Gets the opened URLs.</summary>
    public List<Uri> OpenedUrls { get; } = [];

    /// <summary>Gets or sets an optional result handler.</summary>
    public Func<string, IReadOnlyList<string>, ProcessResult>? Handler { get; set; }

    /// <summary>Gets the runs of a tool.</summary>
    /// <param name="tool">The tool file name, for example <c>netsh.exe</c>.</param>
    /// <returns>The argument lists.</returns>
    public IReadOnlyList<IReadOnlyList<string>> RunsOf(string tool) =>
        Runs.Where(r => string.Equals(Path.GetFileName(r.FileName), tool, StringComparison.OrdinalIgnoreCase)).Select(r => r.Arguments).ToList();

    /// <inheritdoc />
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        Runs.Add((fileName, arguments));
        return Handler?.Invoke(fileName, arguments) ?? new ProcessResult(0, string.Empty);
    }

    /// <inheritdoc />
    public void OpenUrl(Uri url) => OpenedUrls.Add(url);
}
