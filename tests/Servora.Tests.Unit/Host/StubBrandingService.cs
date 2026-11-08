using Servora.Application.Common;
using Servora.Application.Configuration;

namespace Servora.Tests.Unit.Host;

/// <summary>Read-only <see cref="IBrandingService"/> stub returning fixed branding.</summary>
/// <param name="info">The branding to return.</param>
internal sealed class StubBrandingService(BrandingInfo info) : IBrandingService
{
    public Task<BrandingInfo> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(info);

    public Task<Result> UpdateAsync(string productName, string? companyName, string accentColor, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Result> UploadLogoAsync(Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Result> RemoveLogoAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<BrandingLogo?> OpenLogoAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
