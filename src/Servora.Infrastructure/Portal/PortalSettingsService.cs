using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Observability;
using Servora.Application.Portal;
using Servora.Application.Security;

namespace Servora.Infrastructure.Portal;

/// <summary>
/// <see cref="IPortalSettingsService"/> backed by the system settings framework. Values are validated by
/// <see cref="PortalSettingsValidator"/> and the setting definitions. Because <see cref="ISystemSettingsService"/> requires
/// <c>admin.settings.manage</c> for writes, the portal keys are written in a child scope whose actor is the current user
/// restricted to exactly that permission – only after <c>portal.manage</c> was verified and only for <c>portal.*</c> keys,
/// so the settings audit trail still names the real user. The hero image is stored as
/// <c>&lt;data dir&gt;/portal/hero.bin</c> after its type was verified from the content.
/// </summary>
/// <param name="settings">The settings service.</param>
/// <param name="scopeFactory">The scope factory (for the narrowly elevated settings write).</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="paths">The application paths.</param>
/// <param name="audit">The audit service.</param>
public sealed class PortalSettingsService(
    ISystemSettingsService settings,
    IServiceScopeFactory scopeFactory,
    ICurrentUser currentUser,
    IAppPaths paths,
    IAuditService audit) : IPortalSettingsService
{
    private string HeroFile => Path.Combine(paths.DataDirectory, "portal", "hero.bin");

    /// <inheritdoc />
    public async Task<Result<PortalSettingsModel>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PortalSettingsModel>.Forbidden();
        }

        var values = await settings.GetValuesAsync(PortalSettingKeys.Prefix, cancellationToken);
        string Get(string key) => values.GetValueOrDefault(key) ?? string.Empty;
        var imageType = Get(PortalSettingKeys.HeroImageType);
        var imageVersion = Get(PortalSettingKeys.HeroImageVersion);
        return Result<PortalSettingsModel>.Success(new PortalSettingsModel
        {
            HeroGreeting = Get(PortalSettingKeys.HeroGreeting),
            HeroTitle = Get(PortalSettingKeys.HeroTitle),
            HeroSubtitle = Get(PortalSettingKeys.HeroSubtitle),
            SearchPlaceholder = Get(PortalSettingKeys.SearchPlaceholder),
            FrequentSearches = Get(PortalSettingKeys.FrequentSearches),
            HeroImageCaption = Get(PortalSettingKeys.HeroImageCaption),
            HeroImageUrl = imageType.Length > 0 && imageVersion.Length > 0 && File.Exists(HeroFile)
                ? $"{IPortalSettingsService.HeroImagePath}?v={Uri.EscapeDataString(imageVersion)}"
                : null,
            SupportEyebrow = Get(PortalSettingKeys.SupportEyebrow),
            SupportTitle = Get(PortalSettingKeys.SupportTitle),
            SupportText = Get(PortalSettingKeys.SupportText),
            SupportLinkText = Get(PortalSettingKeys.SupportLinkText),
            SupportUrl = Get(PortalSettingKeys.SupportUrl),
            SupportHours = Get(PortalSettingKeys.SupportHours),
            FooterTagline = Get(PortalSettingKeys.FooterTagline),
            ContactUrl = Get(PortalSettingKeys.ContactUrl),
            PrivacyUrl = Get(PortalSettingKeys.PrivacyUrl),
            AccessibilityUrl = Get(PortalSettingKeys.AccessibilityUrl),
            Currency = Get(PortalSettingKeys.Currency) is { Length: > 0 } currency ? currency : "EUR",
        });
    }

    /// <inheritdoc />
    public async Task<Result> SaveAsync(PortalSettingsModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Portal.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (PortalSettingsValidator.Validate(model) is { } error)
        {
            return Result.Invalid(error);
        }

        var result = await WriteAsync(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PortalSettingKeys.HeroGreeting] = model.HeroGreeting,
            [PortalSettingKeys.HeroTitle] = model.HeroTitle,
            [PortalSettingKeys.HeroSubtitle] = model.HeroSubtitle,
            [PortalSettingKeys.SearchPlaceholder] = model.SearchPlaceholder,
            [PortalSettingKeys.FrequentSearches] = string.Join('\n', model.FrequentSearchTerms),
            [PortalSettingKeys.HeroImageCaption] = model.HeroImageCaption,
            [PortalSettingKeys.SupportEyebrow] = model.SupportEyebrow,
            [PortalSettingKeys.SupportTitle] = model.SupportTitle,
            [PortalSettingKeys.SupportText] = model.SupportText,
            [PortalSettingKeys.SupportLinkText] = model.SupportLinkText,
            [PortalSettingKeys.SupportUrl] = model.SupportUrl,
            [PortalSettingKeys.SupportHours] = model.SupportHours,
            [PortalSettingKeys.FooterTagline] = model.FooterTagline,
            [PortalSettingKeys.ContactUrl] = model.ContactUrl,
            [PortalSettingKeys.PrivacyUrl] = model.PrivacyUrl,
            [PortalSettingKeys.AccessibilityUrl] = model.AccessibilityUrl,
            [PortalSettingKeys.Currency] = model.Currency,
        }, cancellationToken);
        if (result.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry("portal", "portal.settings.updated") { TargetType = "portal_settings", TargetName = "Portal settings" }, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result> UploadHeroImageAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!await currentUser.HasPermissionAsync(Permissions.Portal.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > IPortalSettingsService.MaxHeroImageBytes)
            {
                return Result.Invalid($"The image must not exceed {IPortalSettingsService.MaxHeroImageBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        var bytes = buffer.ToArray();
        var contentType = LogoImageValidator.DetectContentType(bytes.AsSpan(0, Math.Min(bytes.Length, LogoImageValidator.HeaderLength)));
        if (contentType is null)
        {
            return Result.Invalid("Upload a PNG, JPEG, GIF or WebP image. SVG files are not accepted.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(HeroFile)!);
        var temp = HeroFile + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
        File.Move(temp, HeroFile, overwrite: true);

        var result = await WriteAsync(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PortalSettingKeys.HeroImageType] = contentType,
            [PortalSettingKeys.HeroImageVersion] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
        }, cancellationToken);
        if (result.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry("portal", "portal.hero_image.updated")
            {
                TargetType = "portal_settings",
                TargetName = "Hero image",
                Message = $"Hero image replaced ({contentType}, {bytes.Length / 1024.0:0.#} KB).",
            }, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result> RemoveHeroImageAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Portal.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (File.Exists(HeroFile))
        {
            File.Delete(HeroFile);
        }

        var result = await WriteAsync(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PortalSettingKeys.HeroImageType] = string.Empty,
            [PortalSettingKeys.HeroImageVersion] = string.Empty,
        }, cancellationToken);
        if (result.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry("portal", "portal.hero_image.removed") { TargetType = "portal_settings", TargetName = "Hero image" }, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<PortalHeroImage>> OpenHeroImageAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PortalHeroImage>.Forbidden();
        }

        var contentType = await settings.GetStringAsync(PortalSettingKeys.HeroImageType, cancellationToken);
        var version = await settings.GetStringAsync(PortalSettingKeys.HeroImageVersion, cancellationToken);
        if (!contentType.StartsWith("image/", StringComparison.Ordinal) || !File.Exists(HeroFile))
        {
            return Result<PortalHeroImage>.NotFound();
        }

        var stream = new FileStream(HeroFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 16 * 1024, useAsync: true);
        return Result<PortalHeroImage>.Success(new PortalHeroImage(stream, contentType, version));
    }

    private async Task<bool> CanReadAsync(CancellationToken cancellationToken) =>
        await currentUser.HasPermissionAsync(Permissions.Portal.Access, cancellationToken)
        || await currentUser.HasPermissionAsync(Permissions.Portal.Manage, cancellationToken);

    private async Task<Result> WriteAsync(IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken)
    {
        if (values.Keys.Any(k => !k.StartsWith(PortalSettingKeys.Prefix, StringComparison.Ordinal)))
        {
            return Result.Invalid("Only portal settings can be changed here.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var actor = new ActorInfo(
            ActorType.User,
            currentUser.Name,
            currentUser.DisplayName,
            currentUser.UserId,
            new HashSet<string>(StringComparer.Ordinal) { Permissions.Configuration.ManageSettings });
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(actor, currentUser.CorrelationId);
        return await scope.ServiceProvider.GetRequiredService<ISystemSettingsService>().SetManyAsync(values, cancellationToken);
    }
}
