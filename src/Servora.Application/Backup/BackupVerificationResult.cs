namespace Servora.Application.Backup;

/// <summary>Result of verifying a backup archive.</summary>
/// <param name="IsValid">Whether every check passed.</param>
/// <param name="EntriesChecked">The number of archive entries whose hash was checked.</param>
/// <param name="BytesChecked">The number of bytes hashed.</param>
/// <param name="SignatureChecked">Whether the manifest signature of an encrypted archive was verified with the password.</param>
/// <param name="Problems">User-safe descriptions of failed checks.</param>
public sealed record BackupVerificationResult(bool IsValid, int EntriesChecked, long BytesChecked, bool SignatureChecked, IReadOnlyList<string> Problems);
