using Servora.Application.Approvals;
using Servora.Application.Common;

namespace Servora.Infrastructure.Placeholders;

/// <summary>Placeholder <see cref="IApprovalService"/> until the Service Management module (T-05) is registered.</summary>
/// <remarks>Placeholder registered by the core kernel so every module can run in isolation. The owning module registers the real implementation later in the DI container, which takes precedence.</remarks>
public sealed class UnavailableApprovalService : IApprovalService
{
    /// <inheritdoc />
    public Task<Result> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure("Approvals are provided by the Service Management module, which is not registered."));

    /// <inheritdoc />
    public Task<ApprovalSummary> GetSummaryAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApprovalSummary(ApprovalState.NotRequested, 0, 0, 0));

    /// <inheritdoc />
    public Task<Result> CancelAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}
