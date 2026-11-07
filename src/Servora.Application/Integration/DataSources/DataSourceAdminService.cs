using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Integration;

namespace Servora.Application.Integration.DataSources;

/// <summary>Default <see cref="IDataSourceAdminService"/>.</summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="executor">The executor (test calls).</param>
/// <param name="allowList">The allow-list provider (cache invalidation).</param>
/// <param name="cache">The memory cache (OAuth token invalidation).</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed partial class DataSourceAdminService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    ISecretStore secrets,
    DataSourceExecutor executor,
    INetworkAllowListProvider allowList,
    IMemoryCache cache,
    IAuditService audit,
    IClock clock) : IDataSourceAdminService
{
    private const string AuditCategory = "integration";

    /// <inheritdoc />
    public async Task<Result<PagedResult<DataSourceListItem>>> ListAsync(PageRequest page, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PagedResult<DataSourceListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var sources = db.Set<DataSource>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var search = page.Search.Trim();
            sources = sources.Where(s => s.Name.Contains(search) || s.BaseUrl.Contains(search));
        }

        sources = page.SortDescending ? sources.OrderByDescending(s => s.Name) : sources.OrderBy(s => s.Name);
        var result = await sources
            .Select(s => new DataSourceListItem(s.Id, s.Name, s.Description, s.BaseUrl, s.AuthType, s.IsEnabled, s.AllowHttp))
            .ToPagedResultAsync(page, cancellationToken);
        var since = clock.UtcNow.AddHours(-24);
        var items = new List<DataSourceListItem>(result.Items.Count);
        foreach (var item in result.Items)
        {
            var logs = await db.Set<DataSourceExecutionLog>().AsNoTracking()
                .Where(l => l.DataSourceId == item.Id && l.StartedAt >= since)
                .Select(l => l.Succeeded)
                .Take(10_000)
                .ToListAsync(cancellationToken);
            var last = await db.Set<DataSourceExecutionLog>().AsNoTracking()
                .Where(l => l.DataSourceId == item.Id)
                .OrderByDescending(l => l.StartedAt)
                .Select(l => (DateTimeOffset?)l.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);
            items.Add(item with
            {
                EndpointCount = await db.Set<DataSourceEndpoint>().CountAsync(e => e.DataSourceId == item.Id, cancellationToken),
                Calls24h = logs.Count,
                Errors24h = logs.Count(s => !s),
                LastCallAt = last,
            });
        }

        return Result<PagedResult<DataSourceListItem>>.Success(result with { Items = items });
    }

    /// <inheritdoc />
    public async Task<Result<DataSourceOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<DataSourceOverview>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var since = clock.UtcNow.AddHours(-24);
        var logs = await db.Set<DataSourceExecutionLog>().AsNoTracking()
            .Where(l => l.StartedAt >= since)
            .OrderByDescending(l => l.StartedAt)
            .Take(20_000)
            .Select(l => new { l.StartedAt, l.Succeeded, l.DurationMs })
            .ToListAsync(cancellationToken);
        var hourly = Enumerable.Range(0, 24).Select(i => (double)logs.Count(l => l.StartedAt >= since.AddHours(i) && l.StartedAt < since.AddHours(i + 1))).ToList();
        return Result<DataSourceOverview>.Success(new DataSourceOverview(
            await db.Set<DataSource>().CountAsync(cancellationToken),
            await db.Set<DataSource>().CountAsync(s => s.IsEnabled, cancellationToken),
            await db.Set<DataSourceEndpoint>().CountAsync(cancellationToken),
            logs.Count,
            logs.Count == 0 ? 0 : Math.Round(100.0 * logs.Count(l => !l.Succeeded) / logs.Count, 1),
            logs.Count == 0 ? 0 : (long)logs.Average(l => l.DurationMs),
            hourly));
    }

    /// <inheritdoc />
    public async Task<Result<DataSourceModel>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<DataSourceModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Set<DataSource>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source is null)
        {
            return Result<DataSourceModel>.NotFound("The data source was not found.");
        }

        var credentialName = CredentialName(source);
        return Result<DataSourceModel>.Success(new DataSourceModel
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            BaseUrl = source.BaseUrl,
            AuthType = source.AuthType,
            ApiKeyHeaderName = source.ApiKeyHeaderName,
            BasicUserName = source.BasicUserName,
            OAuthTokenUrl = source.OAuthTokenUrl,
            OAuthClientId = source.OAuthClientId,
            OAuthScope = source.OAuthScope,
            DefaultHeaders = DataSourceHeaderRules.Parse(source.DefaultHeadersJson).ToDictionary(p => p.Key, p => p.Value),
            TimeoutSeconds = source.TimeoutSeconds,
            MaxRetries = source.MaxRetries,
            MaxResponseBytes = source.MaxResponseBytes,
            RateLimitPerMinute = source.RateLimitPerMinute,
            AllowHttp = source.AllowHttp,
            IsEnabled = source.IsEnabled,
            CredentialConfigured = credentialName is null || await secrets.ExistsAsync(credentialName, cancellationToken),
            ConcurrencyVersion = source.Version,
            UpdatedAt = source.UpdatedAt,
            UpdatedBy = source.UpdatedBy,
        });
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(DataSourceModel model, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (Validate(model) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var name = model.Name.Trim();
        if (await db.Set<DataSource>().AnyAsync(s => s.Name == name && s.Id != model.Id, cancellationToken))
        {
            return Result<Guid>.Conflict($"A data source named '{name}' already exists.");
        }

        DataSource? source;
        var created = model.Id == Guid.Empty;
        if (created)
        {
            source = new DataSource();
            db.Set<DataSource>().Add(source);
        }
        else
        {
            source = await db.Set<DataSource>().FirstOrDefaultAsync(s => s.Id == model.Id, cancellationToken);
            if (source is null)
            {
                return Result<Guid>.NotFound("The data source was not found.");
            }

            if (source.Version != model.ConcurrencyVersion)
            {
                return Result<Guid>.Conflict("The data source was changed by someone else. Reload it and try again.");
            }
        }

        source.Name = name;
        source.Description = FlowRedactor.Truncate(model.Description?.Trim(), 2000);
        source.BaseUrl = model.BaseUrl.Trim().TrimEnd('/');
        source.AuthType = model.AuthType;
        source.ApiKeyHeaderName = string.IsNullOrWhiteSpace(model.ApiKeyHeaderName) ? null : model.ApiKeyHeaderName.Trim();
        source.BasicUserName = string.IsNullOrWhiteSpace(model.BasicUserName) ? null : model.BasicUserName.Trim();
        source.OAuthTokenUrl = string.IsNullOrWhiteSpace(model.OAuthTokenUrl) ? null : model.OAuthTokenUrl.Trim();
        source.OAuthClientId = string.IsNullOrWhiteSpace(model.OAuthClientId) ? null : model.OAuthClientId.Trim();
        source.OAuthScope = string.IsNullOrWhiteSpace(model.OAuthScope) ? null : model.OAuthScope.Trim();
        source.DefaultHeadersJson = model.DefaultHeaders.Count == 0 ? null : JsonSerializer.Serialize(model.DefaultHeaders);
        source.TimeoutSeconds = Math.Clamp(model.TimeoutSeconds, 1, 120);
        source.MaxRetries = Math.Clamp(model.MaxRetries, 0, 5);
        source.MaxResponseBytes = Math.Clamp(model.MaxResponseBytes, 1024, 10 * 1024 * 1024);
        source.RateLimitPerMinute = Math.Clamp(model.RateLimitPerMinute, 0, 10_000);
        source.AllowHttp = model.AllowHttp;
        source.IsEnabled = model.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(DataSourceSecretNames.OAuthTokenCacheKey(source.Id));
        await audit.WriteAsync(new AuditEntry(AuditCategory, created ? "datasource.created" : "datasource.updated")
        {
            TargetType = "data_source",
            TargetId = source.Id.ToString(),
            TargetName = source.Name,
            Details = new { source.BaseUrl, AuthType = source.AuthType.ToString(), source.AllowHttp, source.IsEnabled },
        }, cancellationToken);
        return Result<Guid>.Success(source.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Set<DataSource>().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source is null)
        {
            return Result.NotFound("The data source was not found.");
        }

        db.Set<DataSource>().Remove(source);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var name in DataSourceSecretNames.All(id))
        {
            await secrets.DeleteAsync(name, cancellationToken);
        }

        cache.Remove(DataSourceSecretNames.OAuthTokenCacheKey(id));
        await audit.WriteAsync(new AuditEntry(AuditCategory, "datasource.deleted") { TargetType = "data_source", TargetId = id.ToString(), TargetName = source.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SetCredentialAsync(Guid id, string value, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        if (string.IsNullOrEmpty(value) || value.Length > 8000 || value.Any(c => c is '\r' or '\n'))
        {
            return Result.Invalid("Enter a valid credential (single line, at most 8,000 characters).");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Set<DataSource>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source is null)
        {
            return Result.NotFound("The data source was not found.");
        }

        if (CredentialName(source) is not { } secretName)
        {
            return Result.Invalid("The selected authentication type does not use a stored credential.");
        }

        await secrets.SetAsync(secretName, value, $"Credential of data source '{source.Name}'", cancellationToken);
        cache.Remove(DataSourceSecretNames.OAuthTokenCacheKey(id));
        await audit.WriteAsync(new AuditEntry(AuditCategory, "datasource.credential.set") { TargetType = "data_source", TargetId = id.ToString(), TargetName = source.Name, Message = $"Credential for {source.AuthType} stored." }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ClearCredentialsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        foreach (var name in DataSourceSecretNames.All(id))
        {
            await secrets.DeleteAsync(name, cancellationToken);
        }

        cache.Remove(DataSourceSecretNames.OAuthTokenCacheKey(id));
        await audit.WriteAsync(new AuditEntry(AuditCategory, "datasource.credential.cleared") { TargetType = "data_source", TargetId = id.ToString() }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DataSourceEndpointModel>>> ListEndpointsAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<IReadOnlyList<DataSourceEndpointModel>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var endpoints = await db.Set<DataSourceEndpoint>().AsNoTracking()
            .Where(e => e.DataSourceId == dataSourceId)
            .OrderBy(e => e.Name)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<DataSourceEndpointModel>>.Success(endpoints.Select(e => new DataSourceEndpointModel
        {
            Id = e.Id,
            DataSourceId = e.DataSourceId,
            Key = e.Key,
            Name = e.Name,
            Description = e.Description,
            Method = e.Method,
            PathTemplate = e.PathTemplate,
            Parameters = DataSourceRequestBuilder.ParseParameters(e.ParametersJson),
            BodyTemplate = e.BodyTemplate,
            ResponsePath = e.ResponsePath,
            IsEnabled = e.IsEnabled,
        }).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveEndpointAsync(DataSourceEndpointModel model, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (ValidateEndpoint(model) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Set<DataSource>().AnyAsync(s => s.Id == model.DataSourceId, cancellationToken))
        {
            return Result<Guid>.NotFound("The data source was not found.");
        }

        var key = model.Key.Trim().ToLowerInvariant();
        if (await db.Set<DataSourceEndpoint>().AnyAsync(e => e.DataSourceId == model.DataSourceId && e.Key == key && e.Id != model.Id, cancellationToken))
        {
            return Result<Guid>.Conflict($"An endpoint with the key '{key}' already exists.");
        }

        DataSourceEndpoint? endpoint;
        var created = model.Id == Guid.Empty;
        if (created)
        {
            endpoint = new DataSourceEndpoint { DataSourceId = model.DataSourceId };
            db.Set<DataSourceEndpoint>().Add(endpoint);
        }
        else
        {
            endpoint = await db.Set<DataSourceEndpoint>().FirstOrDefaultAsync(e => e.Id == model.Id && e.DataSourceId == model.DataSourceId, cancellationToken);
            if (endpoint is null)
            {
                return Result<Guid>.NotFound("The endpoint was not found.");
            }
        }

        endpoint.Key = key;
        endpoint.Name = model.Name.Trim();
        endpoint.Description = FlowRedactor.Truncate(model.Description?.Trim(), 2000);
        endpoint.Method = model.Method.Trim().ToUpperInvariant();
        endpoint.PathTemplate = string.IsNullOrWhiteSpace(model.PathTemplate) ? "/" : model.PathTemplate.Trim();
        endpoint.ParametersJson = FlowJson.Serialize(model.Parameters);
        endpoint.BodyTemplate = string.IsNullOrWhiteSpace(model.BodyTemplate) ? null : model.BodyTemplate.Trim();
        endpoint.ResponsePath = string.IsNullOrWhiteSpace(model.ResponsePath) ? null : model.ResponsePath.Trim();
        endpoint.IsEnabled = model.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, created ? "datasource.endpoint.created" : "datasource.endpoint.updated")
        {
            TargetType = "data_source_endpoint",
            TargetId = endpoint.Id.ToString(),
            TargetName = endpoint.Key,
            Details = new { endpoint.Method, endpoint.PathTemplate, Parameters = model.Parameters.Select(p => p.Name).ToList() },
        }, cancellationToken);
        return Result<Guid>.Success(endpoint.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteEndpointAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var endpoint = await db.Set<DataSourceEndpoint>().FirstOrDefaultAsync(e => e.Id == endpointId, cancellationToken);
        if (endpoint is null)
        {
            return Result.NotFound("The endpoint was not found.");
        }

        db.Set<DataSourceEndpoint>().Remove(endpoint);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "datasource.endpoint.deleted") { TargetType = "data_source_endpoint", TargetId = endpointId.ToString(), TargetName = endpoint.Key }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<DataSourceInvocationResult>> TestEndpointAsync(Guid dataSourceId, string endpointKey, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<DataSourceInvocationResult>.Forbidden();
        }

        var cleaned = parameters.Where(p => !string.IsNullOrEmpty(p.Value)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        return await executor.ExecuteDetailedAsync(dataSourceId, endpointKey, cleaned, isTest: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<DataSourceLogItem>>> ListLogsAsync(PageRequest page, Guid? dataSourceId, bool failuresOnly, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PagedResult<DataSourceLogItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var logs = db.Set<DataSourceExecutionLog>().AsNoTracking();
        if (dataSourceId is { } id)
        {
            logs = logs.Where(l => l.DataSourceId == id);
        }

        if (failuresOnly)
        {
            logs = logs.Where(l => !l.Succeeded);
        }

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var search = page.Search.Trim();
            logs = logs.Where(l => l.EndpointKey.Contains(search) || l.DataSourceName.Contains(search) || (l.Error != null && l.Error.Contains(search)));
        }

        var result = await logs.OrderByDescending(l => l.StartedAt)
            .Select(l => new DataSourceLogItem(l.Id, l.DataSourceId, l.DataSourceName, l.EndpointKey, l.Method, l.Host, l.StartedAt, l.DurationMs, l.StatusCode, l.Succeeded, l.ResponseBytes, l.Truncated, l.Attempts, l.Error, l.ActorName, l.IsTest))
            .ToPagedResultAsync(page, cancellationToken);
        return Result<PagedResult<DataSourceLogItem>>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<NetworkAllowListItem>>> ListAllowListAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<IReadOnlyList<NetworkAllowListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entries = await db.Set<NetworkAllowListEntry>().AsNoTracking().OrderBy(e => e.Cidr).Take(500)
            .Select(e => new NetworkAllowListItem(e.Id, e.Cidr, e.Description, e.IsEnabled, e.CreatedAt, e.CreatedBy))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<NetworkAllowListItem>>.Success(entries);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> AddAllowListEntryAsync(string cidr, string? description, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (!NetworkAllowListProvider.TryParse(cidr, out var network))
        {
            return Result<Guid>.Invalid("Enter a valid IP address or CIDR range, for example 10.20.0.0/16.");
        }

        if (network.PrefixLength < (network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 8 : 32))
        {
            return Result<Guid>.Invalid("The range is too broad. Allow only the internal networks that host your APIs.");
        }

        var normalized = network.ToString();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Set<NetworkAllowListEntry>().AnyAsync(e => e.Cidr == normalized, cancellationToken))
        {
            return Result<Guid>.Conflict($"The range {normalized} is already on the allow-list.");
        }

        var entry = new NetworkAllowListEntry { Cidr = normalized, Description = FlowRedactor.Truncate(description?.Trim(), 500), IsEnabled = true };
        db.Set<NetworkAllowListEntry>().Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        allowList.Invalidate();
        await audit.WriteAsync(new AuditEntry(AuditCategory, "network_allow_list.added") { TargetType = "network_allow_list", TargetId = entry.Id.ToString(), TargetName = normalized, Message = entry.Description }, cancellationToken);
        return Result<Guid>.Success(entry.Id);
    }

    /// <inheritdoc />
    public async Task<Result> SetAllowListEntryEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Set<NetworkAllowListEntry>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null)
        {
            return Result.NotFound("The entry was not found.");
        }

        entry.IsEnabled = enabled;
        await db.SaveChangesAsync(cancellationToken);
        allowList.Invalidate();
        await audit.WriteAsync(new AuditEntry(AuditCategory, enabled ? "network_allow_list.enabled" : "network_allow_list.disabled") { TargetType = "network_allow_list", TargetId = id.ToString(), TargetName = entry.Cidr }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAllowListEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Set<NetworkAllowListEntry>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null)
        {
            return Result.NotFound("The entry was not found.");
        }

        db.Set<NetworkAllowListEntry>().Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        allowList.Invalidate();
        await audit.WriteAsync(new AuditEntry(AuditCategory, "network_allow_list.removed") { TargetType = "network_allow_list", TargetId = id.ToString(), TargetName = entry.Cidr }, cancellationToken);
        return Result.Success();
    }

    /// <summary>Validates a data source configuration.</summary>
    /// <param name="model">The model.</param>
    /// <returns>An error message or <see langword="null"/>.</returns>
    public static string? Validate(DataSourceModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 200)
        {
            return "Enter a name of up to 200 characters.";
        }

        if (DataSourceRequestBuilder.ValidateBaseUrl(model.BaseUrl, model.AllowHttp, out _) is { } urlError)
        {
            return urlError;
        }

        if (model.BaseUrl.Length > 1000)
        {
            return "The base URL is too long.";
        }

        if (model.DefaultHeaders.Count > DataSourceHeaderRules.MaxHeaders)
        {
            return $"At most {DataSourceHeaderRules.MaxHeaders} default headers are allowed.";
        }

        foreach (var header in model.DefaultHeaders)
        {
            if (DataSourceHeaderRules.Validate(header.Key, header.Value) is { } headerError)
            {
                return headerError;
            }
        }

        switch (model.AuthType)
        {
            case DataSourceAuthType.ApiKeyHeader when DataSourceHeaderRules.Validate(model.ApiKeyHeaderName, "x") is { } headerError
                && !string.Equals(model.ApiKeyHeaderName, "Authorization", StringComparison.OrdinalIgnoreCase):
                return headerError;
            case DataSourceAuthType.Basic when model.AllowHttp:
                return "Basic authentication is only allowed over HTTPS. Disable 'Allow HTTP'.";
            case DataSourceAuthType.Basic when string.IsNullOrWhiteSpace(model.BasicUserName):
                return "Enter the user name for basic authentication.";
            case DataSourceAuthType.OAuth2ClientCredentials when !Uri.TryCreate(model.OAuthTokenUrl, UriKind.Absolute, out var tokenUri) || tokenUri.Scheme != Uri.UriSchemeHttps:
                return "The OAuth token URL must be an absolute HTTPS URL.";
            case DataSourceAuthType.OAuth2ClientCredentials when string.IsNullOrWhiteSpace(model.OAuthClientId):
                return "Enter the OAuth client id.";
        }

        return null;
    }

    /// <summary>Validates an endpoint definition.</summary>
    /// <param name="model">The model.</param>
    /// <returns>An error message or <see langword="null"/>.</returns>
    public static string? ValidateEndpoint(DataSourceEndpointModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Key) || !EndpointKeyRegex().IsMatch(model.Key.Trim().ToLowerInvariant()))
        {
            return "The key must start with a letter and contain only lower case letters, digits, dots, dashes and underscores (max 100).";
        }

        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 200)
        {
            return "Enter a name of up to 200 characters.";
        }

        if (!DataSourceRequestBuilder.AllowedMethods.Contains(model.Method?.Trim().ToUpperInvariant() ?? string.Empty))
        {
            return "Choose GET, POST, PUT, PATCH or DELETE.";
        }

        if (DataSourceRequestBuilder.ValidatePathTemplate(model.PathTemplate) is { } pathError)
        {
            return pathError;
        }

        if (model.Parameters.Count > 50)
        {
            return "An endpoint can have at most 50 parameters.";
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in model.Parameters)
        {
            if (!DataSourceRequestBuilder.IsValidParameterName(parameter.Name))
            {
                return $"The parameter name '{parameter.Name}' is invalid.";
            }

            if (!names.Add(parameter.Name))
            {
                return $"The parameter '{parameter.Name}' is defined twice.";
            }

            if (parameter.Type == EndpointParameterType.Enum && parameter.AllowedValues.Count == 0)
            {
                return $"The enum parameter '{parameter.Name}' needs allowed values.";
            }

            if (!string.IsNullOrWhiteSpace(parameter.Pattern))
            {
                try
                {
                    _ = new Regex(parameter.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException)
                {
                    return $"The pattern of parameter '{parameter.Name}' is not a valid regular expression.";
                }
            }

            if (parameter.Location == EndpointParameterLocation.Path && !(model.PathTemplate ?? string.Empty).Contains("{" + parameter.Name + "}", StringComparison.Ordinal))
            {
                return $"The path parameter '{parameter.Name}' is not used in the path template.";
            }
        }

        if (!string.IsNullOrWhiteSpace(model.BodyTemplate))
        {
            if (model.BodyTemplate.Length > 16_000)
            {
                return "The body template is too long.";
            }

            var probe = Regex.Replace(model.BodyTemplate, @"\{\{\s*[A-Za-z0-9_.\-]{1,64}\s*\}\}", "0", RegexOptions.None, TimeSpan.FromMilliseconds(250));
            try
            {
                using var _ = JsonDocument.Parse(probe.Replace("\"0\"", "\"x\"", StringComparison.Ordinal));
            }
            catch (JsonException)
            {
                return "The body template must be valid JSON (placeholders like \"{{name}}\" are allowed).";
            }
        }

        return null;
    }

    private static string? CredentialName(DataSource source) => source.AuthType switch
    {
        DataSourceAuthType.ApiKeyHeader => DataSourceSecretNames.ApiKey(source.Id),
        DataSourceAuthType.Bearer => DataSourceSecretNames.BearerToken(source.Id),
        DataSourceAuthType.Basic => DataSourceSecretNames.BasicPassword(source.Id),
        DataSourceAuthType.OAuth2ClientCredentials => DataSourceSecretNames.OAuthClientSecret(source.Id),
        _ => null,
    };

    private Task<bool> CanReadAsync(CancellationToken cancellationToken) => currentUser.HasPermissionAsync(Permissions.Integration.ReadDataSources, cancellationToken);

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) => currentUser.HasPermissionAsync(Permissions.Integration.ManageDataSources, cancellationToken);

    [GeneratedRegex("^[a-z][a-z0-9_.\\-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex EndpointKeyRegex();
}
