using Servora.Application.Common;

namespace Servora.Application.Configuration;

/// <summary>
/// Branding of the instance (product name, company name, logo, accent color) for layouts, e-mails and the sign-in page.
/// </summary>
public interface IBrandingService
{
    /// <summary>The maximum logo size in bytes (512 KB).</summary>
    const int MaxLogoBytes = 512 * 1024;

    /// <summary>The relative URL the logo is served at.</summary>
    const string LogoPath = "/branding/logo";

    /// <summary>
    /// Gets the current branding. Never throws and needs no permission: returns <see cref="BrandingInfo.Default"/> while
    /// no database is configured (setup) or when it is unavailable.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The branding.</returns>
    Task<BrandingInfo> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates product name, company name and accent color (<c>admin.settings.manage</c>).</summary>
    /// <param name="productName">The product name.</param>
    /// <param name="companyName">The company name.</param>
    /// <param name="accentColor">The accent color.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(string productName, string? companyName, string accentColor, CancellationToken cancellationToken = default);

    /// <summary>Validates (type sniffing, size) and stores a new logo (<c>admin.settings.manage</c>).</summary>
    /// <param name="content">The uploaded content; at most <see cref="MaxLogoBytes"/> bytes are read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UploadLogoAsync(Stream content, CancellationToken cancellationToken = default);

    /// <summary>Removes the custom logo (<c>admin.settings.manage</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RemoveLogoAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens the logo for the public logo endpoint.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The logo or <see langword="null"/> when none is configured.</returns>
    Task<BrandingLogo?> OpenLogoAsync(CancellationToken cancellationToken = default);
}
