using Servora.Installer.Abstractions;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>In-memory <see cref="IFileSystem"/> with case-insensitive Windows paths and failure injection.</summary>
internal sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _versions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets a predicate (source, destination) that makes <see cref="CopyFile"/> throw an <see cref="IOException"/>.</summary>
    public Func<string, string, bool>? FailCopy { get; set; }

    /// <summary>Gets all file paths.</summary>
    public IReadOnlyCollection<string> AllFiles => _files.Keys;

    /// <summary>Adds a file (and its parent directories).</summary>
    /// <param name="path">The file path.</param>
    /// <param name="content">The content.</param>
    /// <param name="version">An optional product version.</param>
    public void AddFile(string path, string content, string? version = null)
    {
        path = Normalize(path);
        CreateDirectory(Path.GetDirectoryName(path)!);
        _files[path] = content;
        if (version is not null)
        {
            _versions[path] = version;
        }
        else
        {
            _versions.Remove(path);
        }
    }

    /// <summary>Gets the content of a file, or <see langword="null"/>.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The content.</returns>
    public string? Content(string path) => _files.TryGetValue(Normalize(path), out var content) ? content : null;

    /// <summary>Gets a snapshot of all files below a directory (relative path → content).</summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The snapshot.</returns>
    public IReadOnlyDictionary<string, string> Snapshot(string directory)
    {
        var prefix = Normalize(directory) + "\\";
        return _files
            .Where(f => f.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(f => f.Key[prefix.Length..], f => f.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    /// <inheritdoc />
    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    /// <inheritdoc />
    public void CreateDirectory(string path)
    {
        var current = Normalize(path);
        while (!string.IsNullOrEmpty(current))
        {
            _directories.Add(current);
            var parent = Path.GetDirectoryName(current);
            if (parent is null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = Normalize(parent);
        }
    }

    /// <inheritdoc />
    public void DeleteDirectory(string path)
    {
        var root = Normalize(path);
        var prefix = root + "\\";
        foreach (var file in _files.Keys.Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _files.Remove(file);
            _versions.Remove(file);
        }

        _directories.RemoveWhere(d => string.Equals(d, root, StringComparison.OrdinalIgnoreCase) || d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public void DeleteFile(string path)
    {
        _files.Remove(Normalize(path));
        _versions.Remove(Normalize(path));
    }

    /// <inheritdoc />
    public void CopyFile(string sourcePath, string destinationPath)
    {
        var source = Normalize(sourcePath);
        var destination = Normalize(destinationPath);
        if (!_files.TryGetValue(source, out var content))
        {
            throw new FileNotFoundException("Source missing", source);
        }

        if (!DirectoryExists(Path.GetDirectoryName(destination)!))
        {
            throw new DirectoryNotFoundException(Path.GetDirectoryName(destination));
        }

        if (FailCopy?.Invoke(source, destination) == true)
        {
            throw new IOException($"Simulated failure copying to {destination}");
        }

        _files[destination] = content;
        if (_versions.TryGetValue(source, out var version))
        {
            _versions[destination] = version;
        }
        else
        {
            _versions.Remove(destination);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetFiles(string path)
    {
        var root = Normalize(path);
        return _files.Keys
            .Where(f => string.Equals(Path.GetDirectoryName(f), root, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetDirectories(string path)
    {
        var root = Normalize(path);
        return _directories
            .Where(d => string.Equals(Path.GetDirectoryName(d), root, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public string ReadAllText(string path) =>
        _files.TryGetValue(Normalize(path), out var content) ? content : throw new FileNotFoundException("Missing", path);

    /// <inheritdoc />
    public void WriteAllText(string path, string content) => AddFile(path, content);

    /// <inheritdoc />
    public string? GetProductVersion(string path) =>
        FileExists(path) && _versions.TryGetValue(Normalize(path), out var version) ? version : null;

    private static string Normalize(string path) => path.Length > 3 ? path.TrimEnd('\\') : path;
}
