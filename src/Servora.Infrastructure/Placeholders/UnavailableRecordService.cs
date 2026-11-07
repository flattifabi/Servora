using Servora.Application.Common;
using Servora.Application.Tables;

namespace Servora.Infrastructure.Placeholders;

/// <summary>Placeholder <see cref="IRecordService"/> until the Platform module (T-07) is registered.</summary>
/// <remarks>Placeholder registered by the core kernel so every module can run in isolation. The owning module registers the real implementation later in the DI container, which takes precedence.</remarks>
public sealed class UnavailableRecordService : IRecordService
{
    private const string Message = "Generic record access is provided by the Platform module, which is not registered.";

    /// <inheritdoc />
    public Task<Result<PagedResult<IReadOnlyDictionary<string, object?>>>> QueryAsync(string tableName, RecordQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Failure(Message));

    /// <inheritdoc />
    public Task<Result<IReadOnlyDictionary<string, object?>>> GetAsync(string tableName, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<IReadOnlyDictionary<string, object?>>.Failure(Message));

    /// <inheritdoc />
    public Task<Result<Guid>> CreateAsync(string tableName, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<Guid>.Failure(Message));

    /// <inheritdoc />
    public Task<Result> UpdateAsync(string tableName, Guid id, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure(Message));

    /// <inheritdoc />
    public Task<Result> DeleteAsync(string tableName, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure(Message));
}
