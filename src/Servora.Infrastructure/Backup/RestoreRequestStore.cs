using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Persists the restore request in the data directory. Processing renames it to <c>.processing</c>; a request that was
/// interrupted (crash, power loss) is retried once more, then parked as <c>.failed</c> so a broken restore can never
/// cause an endless restart loop.
/// </summary>
/// <param name="settings">The settings store (file location).</param>
/// <param name="dataProtection">The data protection provider.</param>
public sealed class RestoreRequestStore(BackupSettingsStore settings, IDataProtectionProvider dataProtection)
{
    /// <summary>How often an interrupted restore is started before it is given up.</summary>
    public const int MaxAttempts = 2;

    private const string Purpose = "Servora.Backup.RestoreRequest.v1";
    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    private string ProcessingPath => settings.RestoreRequestPath + ".processing";

    private string FailedPath => settings.RestoreRequestPath + ".failed";

    /// <summary>Gets a value indicating whether a restore is pending.</summary>
    public bool IsPending => File.Exists(settings.RestoreRequestPath) || File.Exists(ProcessingPath);

    /// <summary>Writes a restore request.</summary>
    /// <param name="request">The request (protected fields are filled in).</param>
    /// <param name="password">The archive password.</param>
    /// <param name="safetyPassword">The safety backup password.</param>
    public void Write(RestoreRequest request, string? password, string? safetyPassword)
    {
        request.ProtectedPassword = string.IsNullOrEmpty(password) ? null : _protector.Protect(password);
        request.ProtectedSafetyPassword = string.IsNullOrEmpty(safetyPassword) ? null : _protector.Protect(safetyPassword);
        var temp = settings.RestoreRequestPath + ".tmp";
        File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(request, BackupJson.Options));
        File.Move(temp, settings.RestoreRequestPath, overwrite: true);
    }

    /// <summary>Takes the pending request for processing.</summary>
    /// <returns>The ticket, or <see langword="null"/> when no restore is pending (or it was given up).</returns>
    public RestoreTicket? TryTake()
    {
        if (File.Exists(settings.RestoreRequestPath))
        {
            File.Move(settings.RestoreRequestPath, ProcessingPath, overwrite: true);
        }

        if (!File.Exists(ProcessingPath))
        {
            return null;
        }

        RestoreRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<RestoreRequest>(File.ReadAllBytes(ProcessingPath), BackupJson.Options);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || request.Attempts >= MaxAttempts)
        {
            Fail();
            return null;
        }

        request.Attempts++;
        File.WriteAllBytes(ProcessingPath, JsonSerializer.SerializeToUtf8Bytes(request, BackupJson.Options));
        try
        {
            return new RestoreTicket(request, Unprotect(request.ProtectedPassword), Unprotect(request.ProtectedSafetyPassword));
        }
        catch (CryptographicException)
        {
            Fail();
            throw new BackupException("The restore request cannot be read because its password protection keys are missing.");
        }
    }

    /// <summary>Removes the processed request.</summary>
    public void Complete() => File.Delete(ProcessingPath);

    /// <summary>Parks the request as failed (kept for diagnostics).</summary>
    public void Fail()
    {
        if (File.Exists(ProcessingPath))
        {
            File.Move(ProcessingPath, FailedPath, overwrite: true);
        }
    }

    private string? Unprotect(string? value) => value is null ? null : _protector.Unprotect(value);
}
