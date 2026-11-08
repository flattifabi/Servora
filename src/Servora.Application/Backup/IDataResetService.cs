using Servora.Application.Common;

namespace Servora.Application.Backup;

/// <summary>Removes the records of an instance (for example the sample data) and keeps its configuration.</summary>
public interface IDataResetService
{
    /// <summary>The text an administrator must type to confirm the reset.</summary>
    public const string ConfirmationText = "RESET";

    /// <summary>Counts what a reset would remove.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows per area.</returns>
    Task<Result<DataResetPreview>> GetPreviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a backup and then removes the records. Nothing is removed when the backup fails.</summary>
    /// <param name="confirmation">The typed confirmation, which must equal <see cref="ConfirmationText"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of removed rows and the backup that was created.</returns>
    Task<Result<DataResetResult>> ResetAsync(string confirmation, CancellationToken cancellationToken = default);
}

/// <summary>What a reset would remove.</summary>
/// <param name="Areas">The rows per area.</param>
public sealed record DataResetPreview(IReadOnlyList<DataResetArea> Areas)
{
    /// <summary>Gets the total number of rows.</summary>
    public long TotalRows => Areas.Sum(a => a.Rows);
}

/// <summary>The rows of one functional area.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Rows">The number of rows.</param>
public sealed record DataResetArea(string Name, long Rows);

/// <summary>The outcome of a reset.</summary>
/// <param name="DeletedRows">The number of removed rows.</param>
/// <param name="BackupId">The identifier of the backup created beforehand.</param>
public sealed record DataResetResult(long DeletedRows, string BackupId);
