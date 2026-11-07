using Microsoft.EntityFrameworkCore;
using Servora.Application.Automation.Engine;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Integration;

namespace Servora.Application.Integration.DataSources;

/// <summary>
/// <see cref="IDataSourceExecutor"/> implementation: permission check (<c>integration.datasources.execute</c> or a system
/// actor), enabled source and endpoint, rate limit, execution through <see cref="IDataSourceInvoker"/> and a secret-free
/// execution log entry. Only approved endpoints can be executed; arbitrary URLs never.
/// </summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="invoker">The HTTP invoker.</param>
/// <param name="rateLimiter">The rate limiter.</param>
/// <param name="clock">The clock.</param>
public sealed class DataSourceExecutor(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IDataSourceInvoker invoker,
    DataSourceRateLimiter rateLimiter,
    IClock clock) : IDataSourceExecutor
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DataSourceSummary>> ListAccessibleAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Integration.ExecuteDataSources, cancellationToken))
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var sources = await db.Set<DataSource>().AsNoTracking().Where(s => s.IsEnabled).OrderBy(s => s.Name).Take(200)
            .Select(s => new { s.Id, s.Name, s.Description }).ToListAsync(cancellationToken);
        var endpoints = await db.Set<DataSourceEndpoint>().AsNoTracking()
            .Where(e => e.IsEnabled && db.Set<DataSource>().Any(s => s.Id == e.DataSourceId && s.IsEnabled))
            .OrderBy(e => e.Name)
            .Take(2000)
            .ToListAsync(cancellationToken);
        return sources.Select(s => new DataSourceSummary(
            s.Id,
            s.Name,
            s.Description,
            endpoints.Where(e => e.DataSourceId == s.Id)
                .Select(e => new DataSourceEndpointSummary(e.Key, e.Name, e.Method, e.Description, DataSourceRequestBuilder.ParseParameters(e.ParametersJson).Select(p => p.Name).ToList()))
                .ToList())).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<DataSourceResult>> ExecuteAsync(Guid dataSourceId, string endpointKey, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteDetailedAsync(dataSourceId, endpointKey, parameters, isTest: false, cancellationToken);
        if (!result.Succeeded)
        {
            return Result<DataSourceResult>.From(result);
        }

        var value = result.Value!;
        return Result<DataSourceResult>.Success(new DataSourceResult(value.StatusCode, value.ContentType, value.MappedBody ?? value.Body, value.DurationMs, value.Truncated));
    }

    /// <summary>Executes an endpoint and returns the detailed result (used by the test console, imports and poll triggers).</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <param name="endpointKey">The endpoint key.</param>
    /// <param name="parameters">The parameter values.</param>
    /// <param name="isTest">Whether this is a test from the administration UI (allowed for disabled endpoints).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The detailed result.</returns>
    public async Task<Result<DataSourceInvocationResult>> ExecuteDetailedAsync(Guid dataSourceId, string endpointKey, IReadOnlyDictionary<string, string>? parameters, bool isTest, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Integration.ExecuteDataSources, cancellationToken))
        {
            return Result<DataSourceInvocationResult>.Forbidden();
        }

        DataSource? source;
        DataSourceEndpoint? endpoint;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            source = await db.Set<DataSource>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == dataSourceId, cancellationToken);
            var key = endpointKey?.Trim() ?? string.Empty;
            endpoint = source is null ? null : await db.Set<DataSourceEndpoint>().AsNoTracking().FirstOrDefaultAsync(e => e.DataSourceId == dataSourceId && e.Key == key, cancellationToken);
        }

        if (source is null || endpoint is null)
        {
            return Result<DataSourceInvocationResult>.NotFound("The data source endpoint was not found.");
        }

        if (!isTest && (!source.IsEnabled || !endpoint.IsEnabled))
        {
            return Result<DataSourceInvocationResult>.Invalid("The data source or endpoint is disabled.");
        }

        var startedAt = clock.UtcNow;
        if (!rateLimiter.TryAcquire(source.Id, source.RateLimitPerMinute, startedAt))
        {
            await LogAsync(source, endpoint, startedAt, null, "Rate limit exceeded.", isTest, cancellationToken);
            return Result<DataSourceInvocationResult>.Failure($"The rate limit of {source.RateLimitPerMinute} requests per minute for '{source.Name}' was exceeded.");
        }

        var result = await invoker.InvokeAsync(source, endpoint, parameters ?? new Dictionary<string, string>(), cancellationToken);
        await LogAsync(source, endpoint, startedAt, result.Succeeded ? result.Value : null, result.Succeeded ? null : result.Error, isTest, CancellationToken.None);
        return result;
    }

    private async Task LogAsync(DataSource source, DataSourceEndpoint endpoint, DateTimeOffset startedAt, DataSourceInvocationResult? result, string? error, bool isTest, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<DataSourceExecutionLog>().Add(new DataSourceExecutionLog
        {
            DataSourceId = source.Id,
            DataSourceName = source.Name,
            EndpointKey = endpoint.Key,
            Method = endpoint.Method,
            Host = result?.Host ?? (Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var uri) ? uri.Host : null),
            StartedAt = startedAt,
            DurationMs = result?.DurationMs ?? (long)(clock.UtcNow - startedAt).TotalMilliseconds,
            StatusCode = result?.StatusCode ?? 0,
            Succeeded = result is { StatusCode: >= 200 and < 300 },
            ResponseBytes = result?.ResponseBytes ?? 0,
            Truncated = result?.Truncated ?? false,
            Attempts = result?.Attempts ?? 1,
            Error = FlowRedactor.Truncate(error ?? (result is { StatusCode: >= 400 } ? $"HTTP {result.StatusCode}" : null), 2000),
            ActorName = FlowRedactor.Truncate(currentUser.DisplayName, 256),
            IsTest = isTest,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
