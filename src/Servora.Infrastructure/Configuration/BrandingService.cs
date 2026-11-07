using System.Globalization;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// <see cref="IBrandingService"/> backed by system settings. The logo is stored as <c>&lt;data dir&gt;/branding/logo.bin</c>
/// after its type was verified from the file content; the client-supplied name and content type are ignored.
/// </summary>
/// <param name="settings">The settings service.</param>
/// <param name="settingsStore">The database settings store (branding falls back to defaults during setup).</param>
/// <param name="paths">The application paths.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
/// <param name="logger">The logger.</param>
public sealed class BrandingService(
    ISystemSettingsService settings,
    DatabaseSettingsStore settingsStore,
    IAppPaths paths,
    ICurrentUser currentUser,
    IAuditService audit,
    ILogger<BrandingService> logger) : IBrandingService
{
    private string BrandingDirectory => Path.Combine(paths.DataDirectory, "branding");

    private string LogoFile => Path.Combine(BrandingDirectory, "logo.bin");

    /// <inheritdoc />
    public async Task<BrandingInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!settingsStore.IsConfigured)
        {
            return BrandingInfo.Default;
        }

        try
        {
            var values = await settings.GetValuesAsync("branding.", cancellationToken);
            var productName = values.GetValueOrDefault(SystemSettingKeys.ProductName);
            var accent = values.GetValueOrDefault(SystemSettingKeys.AccentColor);
            var contentType = values.GetValueOrDefault(SystemSettingKeys.LogoContentType);
            var version = values.GetValueOrDefault(SystemSettingKeys.LogoVersion);
            var logoUrl = !string.IsNullOrEmpty(contentType) && !string.IsNullOrEmpty(version) && File.Exists(LogoFile)
                ? $"{IBrandingService.LogoPath}?v={Uri.EscapeDataString(version)}"
                : null;
            return new BrandingInfo(
                string.IsNullOrWhiteSpace(productName) ? BrandingInfo.Default.ProductName : productName,
                values.GetValueOrDefault(SystemSettingKeys.CompanyName) ?? string.Empty,
                string.IsNullOrWhiteSpace(accent) ? BrandingInfo.Default.AccentColor : accent,
                logoUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Branding could not be loaded; using defaults.");
            return BrandingInfo.Default;
        }
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(string productName, string? companyName, string accentColor, CancellationToken cancellationToken = default) =>
        settings.SetManyAsync(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [SystemSettingKeys.ProductName] = productName,
            [SystemSettingKeys.CompanyName] = companyName,
            [SystemSettingKeys.AccentColor] = accentColor,
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> UploadLogoAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result.Forbidden();
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > IBrandingService.MaxLogoBytes)
            {
                return Result.Invalid($"The logo must not exceed {IBrandingService.MaxLogoBytes / 1024} KB.");
            }

            buffer.Write(chunk, 0, read);
        }

        var bytes = buffer.ToArray();
        var contentType = LogoImageValidator.DetectContentType(bytes.AsSpan(0, Math.Min(bytes.Length, LogoImageValidator.HeaderLength)));
        if (contentType is null)
        {
            return Result.Invalid("Upload a PNG, JPEG, GIF or WebP image. SVG files are not accepted.");
        }

        Directory.CreateDirectory(BrandingDirectory);
        var temp = LogoFile + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
        File.Move(temp, LogoFile, overwrite: true);

        var version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var result = await settings.SetManyAsync(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [SystemSettingKeys.LogoContentType] = contentType,
            [SystemSettingKeys.LogoVersion] = version,
        }, cancellationToken);
        if (result.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, "branding.logo.updated")
            {
                TargetType = "branding",
                TargetName = "logo",
                Message = $"Logo replaced ({contentType}, {bytes.Length / 1024.0:0.#} KB).",
            }, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result> RemoveLogoAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (File.Exists(LogoFile))
        {
            File.Delete(LogoFile);
        }

        var result = await settings.SetManyAsync(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [SystemSettingKeys.LogoContentType] = string.Empty,
            [SystemSettingKeys.LogoVersion] = string.Empty,
        }, cancellationToken);
        if (result.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, "branding.logo.removed")
            {
                TargetType = "branding",
                TargetName = "logo",
                Message = "Custom logo removed.",
            }, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<BrandingLogo?> OpenLogoAsync(CancellationToken cancellationToken = default)
    {
        if (!settingsStore.IsConfigured || !File.Exists(LogoFile))
        {
            return null;
        }

        var contentType = await settings.GetStringAsync(SystemSettingKeys.LogoContentType, cancellationToken);
        var version = await settings.GetStringAsync(SystemSettingKeys.LogoVersion, cancellationToken);
        if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("image/", StringComparison.Ordinal))
        {
            return null;
        }

        var stream = new FileStream(LogoFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 16 * 1024, useAsync: true);
        return new BrandingLogo(stream, contentType, version);
    }
}
