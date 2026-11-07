using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// Manages the one-time setup token in <c>&lt;data dir&gt;/setup-token.txt</c>. While the instance is not configured,
/// clients that do not connect from the local machine must enter this token before the setup wizard accepts any input.
/// The token is never logged; only the file location is.
/// </summary>
/// <param name="paths">The application paths.</param>
/// <param name="logger">The logger.</param>
public sealed class SetupTokenStore(IAppPaths paths, ILogger<SetupTokenStore> logger)
{
    /// <summary>The token file name.</summary>
    public const string FileName = "setup-token.txt";

    /// <summary>The minimum accepted length of a token read from the file.</summary>
    internal const int MinimumLength = 32;

    private readonly object _sync = new();
    private string? _token;

    /// <summary>Gets the full path of the token file.</summary>
    public string FilePath => Path.Combine(paths.DataDirectory, FileName);

    /// <summary>Returns the current token, creating and persisting a new random token when none exists.</summary>
    /// <returns>The token.</returns>
    public string EnsureToken()
    {
        lock (_sync)
        {
            if (_token is not null)
            {
                return _token;
            }

            if (File.Exists(FilePath))
            {
                var existing = File.ReadLines(FilePath).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'));
                if (existing is { Length: >= MinimumLength })
                {
                    _token = existing;
                    return _token;
                }
            }

            _token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
            var content = "# Servora setup token. Enter it in the setup wizard when connecting from another machine." + Environment.NewLine
                + "# The file is deleted automatically when setup completes." + Environment.NewLine
                + _token + Environment.NewLine;
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, content, Encoding.UTF8);
            File.Move(temp, FilePath, overwrite: true);
            logger.LogWarning("Servora is not configured yet. A setup token for remote access to the setup wizard was written to {Path}.", FilePath);
            return _token;
        }
    }

    /// <summary>Compares a candidate with the token in constant time.</summary>
    /// <param name="candidate">The token entered by the user.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public bool Validate(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(EnsureToken());
        var actual = Encoding.UTF8.GetBytes(candidate.Trim());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>Deletes the token file and forgets the token (after setup completed).</summary>
    public void Delete()
    {
        lock (_sync)
        {
            _token = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "The setup token file {Path} could not be deleted. Delete it manually.", FilePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "The setup token file {Path} could not be deleted. Delete it manually.", FilePath);
            }
        }
    }
}
