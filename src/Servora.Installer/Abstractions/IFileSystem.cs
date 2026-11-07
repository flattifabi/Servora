namespace Servora.Installer.Abstractions;

/// <summary>File system operations used by the installer workflows (faked in unit tests).</summary>
internal interface IFileSystem
{
    /// <summary>Determines whether a directory exists.</summary>
    /// <param name="path">The directory path.</param>
    /// <returns><see langword="true"/> when the directory exists.</returns>
    bool DirectoryExists(string path);

    /// <summary>Determines whether a file exists.</summary>
    /// <param name="path">The file path.</param>
    /// <returns><see langword="true"/> when the file exists.</returns>
    bool FileExists(string path);

    /// <summary>Creates a directory and all missing parents.</summary>
    /// <param name="path">The directory path.</param>
    void CreateDirectory(string path);

    /// <summary>Deletes a directory recursively. Does nothing when it does not exist.</summary>
    /// <param name="path">The directory path.</param>
    void DeleteDirectory(string path);

    /// <summary>Deletes a file. Does nothing when it does not exist.</summary>
    /// <param name="path">The file path.</param>
    void DeleteFile(string path);

    /// <summary>Copies a file, overwriting the destination.</summary>
    /// <param name="sourcePath">The source file.</param>
    /// <param name="destinationPath">The destination file (its directory must exist).</param>
    void CopyFile(string sourcePath, string destinationPath);

    /// <summary>Lists the full paths of the files directly inside a directory.</summary>
    /// <param name="path">The directory path.</param>
    /// <returns>The file paths.</returns>
    IReadOnlyList<string> GetFiles(string path);

    /// <summary>Lists the full paths of the sub-directories directly inside a directory.</summary>
    /// <param name="path">The directory path.</param>
    /// <returns>The directory paths.</returns>
    IReadOnlyList<string> GetDirectories(string path);

    /// <summary>Reads a UTF-8 text file.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file content.</returns>
    string ReadAllText(string path);

    /// <summary>Writes a UTF-8 text file, replacing an existing one.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="content">The content.</param>
    void WriteAllText(string path, string content);

    /// <summary>Reads the product (informational) version of an executable or assembly.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The version text, or <see langword="null"/> when the file is missing or has no version resource.</returns>
    string? GetProductVersion(string path);
}
