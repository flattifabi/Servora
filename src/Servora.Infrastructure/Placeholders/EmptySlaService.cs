using Servora.Application.Sla;

namespace Servora.Infrastructure.Placeholders;

/// <summary>Placeholder <see cref="ISlaService"/> returning no timers until the Platform module (T-07) is registered.</summary>
/// <remarks>Placeholder registered by the core kernel so every module can run in isolation. The owning module registers the real implementation later in the DI container, which takes precedence.</remarks>
public sealed class EmptySlaService : ISlaService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<TaskSlaInfo>> GetForRecordAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TaskSlaInfo>>([]);
}
