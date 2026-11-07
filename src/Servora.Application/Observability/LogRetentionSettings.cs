namespace Servora.Application.Observability;

/// <summary>Retention and rolling settings of the JSON-lines log files.</summary>
/// <param name="RetentionDays">Days log files are kept (1..365).</param>
/// <param name="MaxFileSizeMb">Maximum size of one file before it rolls over (1..1024 MB).</param>
public sealed record LogRetentionSettings(int RetentionDays = 14, int MaxFileSizeMb = 20)
{
    /// <summary>Validates the settings.</summary>
    /// <returns>An error message, or <see langword="null"/> when valid.</returns>
    public string? Validate() =>
        RetentionDays is < 1 or > 365 ? "Retention must be between 1 and 365 days."
        : MaxFileSizeMb is < 1 or > 1024 ? "The maximum file size must be between 1 and 1024 MB."
        : null;
}
