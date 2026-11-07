using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Copies and clears directory trees through <see cref="IFileSystem"/>. File operations are retried because a
/// stopped service process can hold file locks for a moment after the Service Control Manager reports "stopped".
/// </summary>
/// <param name="fileSystem">The file system.</param>
/// <param name="timings">Retry settings.</param>
internal sealed class FileTreeCopier(IFileSystem fileSystem, InstallerTimings timings)
{
    /// <summary>Copies all files and sub-directories of <paramref name="source"/> into <paramref name="destination"/>.</summary>
    /// <param name="source">The source directory.</param>
    /// <param name="destination">The destination directory (created when missing).</param>
    /// <param name="excludedTopLevelNames">Names of top-level entries of <paramref name="source"/> that are skipped.</param>
    /// <returns>The number of copied files.</returns>
    public int Copy(string source, string destination, IReadOnlySet<string>? excludedTopLevelNames = null)
    {
        fileSystem.CreateDirectory(destination);
        var count = 0;
        foreach (var file in fileSystem.GetFiles(source))
        {
            var name = Path.GetFileName(file);
            if (excludedTopLevelNames?.Contains(name) == true)
            {
                continue;
            }

            var target = Path.Combine(destination, name);
            Retry(() => fileSystem.CopyFile(file, target), $"copy '{name}'");
            count++;
        }

        foreach (var directory in fileSystem.GetDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (excludedTopLevelNames?.Contains(name) == true)
            {
                continue;
            }

            count += Copy(directory, Path.Combine(destination, name));
        }

        return count;
    }

    /// <summary>Deletes the content of a directory, keeping the directory itself and the preserved entries.</summary>
    /// <param name="directory">The directory to clear.</param>
    /// <param name="preservedTopLevelNames">Names of top-level entries that must not be deleted.</param>
    public void Clear(string directory, IReadOnlySet<string> preservedTopLevelNames)
    {
        if (!fileSystem.DirectoryExists(directory))
        {
            return;
        }

        foreach (var file in fileSystem.GetFiles(directory))
        {
            if (!preservedTopLevelNames.Contains(Path.GetFileName(file)))
            {
                Retry(() => fileSystem.DeleteFile(file), $"delete '{Path.GetFileName(file)}'");
            }
        }

        foreach (var sub in fileSystem.GetDirectories(directory))
        {
            if (!preservedTopLevelNames.Contains(Path.GetFileName(sub)))
            {
                Retry(() => fileSystem.DeleteDirectory(sub), $"delete '{Path.GetFileName(sub)}'");
            }
        }
    }

    /// <summary>Deletes a directory tree with retries.</summary>
    /// <param name="directory">The directory.</param>
    public void Delete(string directory)
    {
        if (fileSystem.DirectoryExists(directory))
        {
            Retry(() => fileSystem.DeleteDirectory(directory), $"delete '{directory}'");
        }
    }

    private void Retry(Action action, string description)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < timings.FileRetryCount)
            {
                if (timings.FileRetryDelay > TimeSpan.Zero)
                {
                    Thread.Sleep(timings.FileRetryDelay);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InstallerException($"Could not {description}: {ex.Message}", ex);
            }
        }
    }
}
