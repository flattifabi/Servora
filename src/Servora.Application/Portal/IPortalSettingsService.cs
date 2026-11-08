using Servora.Application.Common;

namespace Servora.Application.Portal;

/// <summary>
/// Configurable texts, links and the hero image of the self-service portal, stored as system settings
/// (<see cref="PortalSettingKeys"/>). Reading requires <c>portal.access</c> or <c>portal.manage</c>; changes require
/// <c>portal.manage</c> and are audited.
/// </summary>
public interface IPortalSettingsService
{
    /// <summary>The maximum hero image size in bytes (2 MB).</summary>
    const int MaxHeroImageBytes = 2 * 1024 * 1024;

    /// <summary>The relative URL the hero image is served at.</summary>
    const string HeroImagePath = "/portal/hero-image";

    /// <summary>Gets the current portal settings.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings.</returns>
    Task<Result<PortalSettingsModel>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and stores the portal settings (all or nothing).</summary>
    /// <param name="model">The settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SaveAsync(PortalSettingsModel model, CancellationToken cancellationToken = default);

    /// <summary>Validates (type sniffing: PNG, JPEG, GIF or WebP; size limit) and stores a new hero image.</summary>
    /// <param name="content">The uploaded content; at most <see cref="MaxHeroImageBytes"/> bytes are read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UploadHeroImageAsync(Stream content, CancellationToken cancellationToken = default);

    /// <summary>Removes the hero image (the built-in illustration is shown again).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RemoveHeroImageAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens the hero image for the image endpoint (<c>portal.access</c> or <c>portal.manage</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image; not found when none is configured.</returns>
    Task<Result<PortalHeroImage>> OpenHeroImageAsync(CancellationToken cancellationToken = default);
}
