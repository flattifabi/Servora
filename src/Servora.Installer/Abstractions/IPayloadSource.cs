namespace Servora.Installer.Abstractions;

/// <summary>A source of the published Servora application (embedded zip, zip file or folder).</summary>
internal interface IPayloadSource
{
    /// <summary>Gets a short operator-facing description (for example <c>embedded payload</c>).</summary>
    string Description { get; }

    /// <summary>Extracts or copies the application files into an empty directory.</summary>
    /// <param name="destinationDirectory">The destination directory (exists and is empty).</param>
    void ExtractTo(string destinationDirectory);
}
