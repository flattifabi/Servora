using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Ai;
using Servora.Domain.Observability;

namespace Servora.Application.Ai;

/// <summary>Default <see cref="IAiConfigurationService"/>.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="resolver">The connection resolver.</param>
/// <param name="chat">The chat client.</param>
/// <param name="orchestrator">The orchestrator (agent catalog).</param>
/// <param name="tools">The registered tools.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class AiConfigurationService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    ISecretStore secrets,
    IAiConnectionResolver resolver,
    IAiChatClient chat,
    IAiOrchestrator orchestrator,
    IEnumerable<IMcpTool> tools,
    IAuditService audit,
    IClock clock) : IAiConfigurationService
{
    /// <summary>The default Azure OpenAI API version.</summary>
    public const string DefaultAzureApiVersion = "2024-10-21";

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<AiProviderSummary>>> ListProvidersAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<AiProviderSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var providers = await db.Set<AiProviderConfig>().AsNoTracking().OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).Take(100).ToListAsync(cancellationToken);
        var result = new List<AiProviderSummary>();
        foreach (var p in providers)
        {
            result.Add(new AiProviderSummary
            {
                Id = p.Id,
                Name = p.Name,
                Kind = p.Kind,
                BaseUrl = p.BaseUrl,
                Model = p.Model,
                Deployment = p.Deployment,
                ApiVersion = p.ApiVersion,
                MaxTokens = p.MaxTokens,
                Temperature = p.Temperature,
                TimeoutSeconds = p.TimeoutSeconds,
                SupportsTools = p.SupportsTools,
                IsEnabled = p.IsEnabled,
                IsDefault = p.IsDefault,
                HasApiKey = await secrets.ExistsAsync(p.SecretName, cancellationToken),
                LastTestedAt = p.LastTestedAt,
                LastTestSucceeded = p.LastTestSucceeded,
                LastTestMessage = p.LastTestMessage,
            });
        }

        return Result<IReadOnlyList<AiProviderSummary>>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveProviderAsync(AiProviderSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var validation = Validate(request);
        if (!validation.Succeeded)
        {
            return Result<Guid>.From(validation);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var name = request.Name.Trim();
        if (await db.Set<AiProviderConfig>().AnyAsync(p => p.Name == name && p.Id != request.Id, cancellationToken))
        {
            return Result<Guid>.Conflict("A provider with this name already exists.");
        }

        AiProviderConfig? provider;
        if (request.Id is { } id)
        {
            provider = await db.Set<AiProviderConfig>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (provider is null)
            {
                return Result<Guid>.NotFound("The provider was not found.");
            }
        }
        else
        {
            provider = new AiProviderConfig();
            db.Set<AiProviderConfig>().Add(provider);
        }

        provider.Name = name;
        provider.Kind = request.Kind;
        provider.BaseUrl = request.BaseUrl.Trim().TrimEnd('/');
        provider.Model = request.Model.Trim();
        provider.Deployment = request.Kind == AiProviderKind.AzureOpenAi ? request.Deployment?.Trim() : null;
        provider.ApiVersion = request.Kind == AiProviderKind.AzureOpenAi ? (string.IsNullOrWhiteSpace(request.ApiVersion) ? DefaultAzureApiVersion : request.ApiVersion.Trim()) : null;
        provider.MaxTokens = request.MaxTokens;
        provider.Temperature = request.Temperature;
        provider.TimeoutSeconds = request.TimeoutSeconds;
        provider.SupportsTools = request.SupportsTools;
        provider.IsEnabled = request.IsEnabled;

        var isFirst = !await db.Set<AiProviderConfig>().AnyAsync(p => p.Id != provider.Id, cancellationToken);
        provider.IsDefault = request.IsDefault || isFirst;
        if (provider.IsDefault)
        {
            foreach (var other in await db.Set<AiProviderConfig>().Where(p => p.IsDefault && p.Id != provider.Id).ToListAsync(cancellationToken))
            {
                other.IsDefault = false;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var keyChange = "unchanged";
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            await secrets.SetAsync(provider.SecretName, request.ApiKey.Trim(), $"API key of AI provider '{provider.Name}'", cancellationToken);
            keyChange = "replaced";
        }
        else if (request.ClearApiKey)
        {
            await secrets.DeleteAsync(provider.SecretName, cancellationToken);
            keyChange = "removed";
        }

        await audit.WriteAsync(
            new AuditEntry("config", request.Id is null ? "ai.provider.created" : "ai.provider.updated")
            {
                TargetType = "ai_provider",
                TargetId = provider.Id.ToString(),
                TargetName = provider.Name,
                Message = $"AI provider '{provider.Name}' saved.",
                Details = new { kind = provider.Kind.ToString(), provider.BaseUrl, provider.Model, provider.IsDefault, provider.IsEnabled, apiKey = keyChange },
            },
            cancellationToken);
        return Result<Guid>.Success(provider.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var provider = await db.Set<AiProviderConfig>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Result.NotFound("The provider was not found.");
        }

        db.Set<AiProviderConfig>().Remove(provider);
        await db.SaveChangesAsync(cancellationToken);
        await secrets.DeleteAsync(provider.SecretName, cancellationToken);
        await audit.WriteAsync(
            new AuditEntry("config", "ai.provider.deleted") { TargetType = "ai_provider", TargetId = id.ToString(), TargetName = provider.Name, Message = $"AI provider '{provider.Name}' deleted." },
            cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<AiProviderTestResult>> TestProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<AiProviderTestResult>.Forbidden();
        }

        var connection = await resolver.GetAsync(id, cancellationToken);
        if (connection is null)
        {
            return Result<AiProviderTestResult>.NotFound("The provider was not found.");
        }

        var stopwatch = Stopwatch.StartNew();
        var response = await chat.CompleteAsync(
            connection,
            new AiChatRequest
            {
                SystemPrompt = "You are a connectivity test. Follow the instruction exactly.",
                Messages = [AiChatMessage.User("Reply with the single word: OK")],
                MaxTokens = 16,
            },
            cancellationToken);
        stopwatch.Stop();

        var result = response.Succeeded
            ? new AiProviderTestResult(true, $"Connected to {connection.Name} in {stopwatch.ElapsedMilliseconds:N0} ms.", stopwatch.ElapsedMilliseconds, McpJson.Truncate(response.Value!.Content.Trim(), 200))
            : new AiProviderTestResult(false, response.Error ?? "The provider did not answer.", stopwatch.ElapsedMilliseconds, null);

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var provider = await db.Set<AiProviderConfig>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (provider is not null)
            {
                provider.LastTestedAt = clock.UtcNow;
                provider.LastTestSucceeded = result.Succeeded;
                provider.LastTestMessage = McpJson.Truncate(result.Message, 990);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        await audit.WriteAsync(
            new AuditEntry("config", "ai.provider.tested", result.Succeeded ? AuditOutcome.Success : AuditOutcome.Failure)
            {
                TargetType = "ai_provider",
                TargetId = id.ToString(),
                TargetName = connection.Name,
                Message = result.Message,
            },
            cancellationToken);
        return Result<AiProviderTestResult>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<AiAgentInfo>>> ListAgentsAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<AiAgentInfo>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var disabled = await db.Set<AiAgentSetting>().AsNoTracking().Where(a => !a.IsEnabled).Select(a => a.AgentKey).ToListAsync(cancellationToken);
        var toolList = tools.ToList();
        IReadOnlyList<AiAgentInfo> agents = orchestrator.Agents.Select(a => new AiAgentInfo(
            a.Key,
            a.Name,
            a.Description,
            a.Icon,
            a.Scope,
            a.ToolNames.Select(n => toolList.FirstOrDefault(t => t.Name == n) is { } t
                ? new AiAgentToolInfo(t.Name, t.Title, t.IsConsequential)
                : new AiAgentToolInfo(n, n, false)).ToList(),
            !disabled.Contains(a.Key))).ToList();
        return Result<IReadOnlyList<AiAgentInfo>>.Success(agents);
    }

    /// <inheritdoc />
    public async Task<Result> SetAgentEnabledAsync(string agentKey, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        if (orchestrator.Agents.All(a => a.Key != agentKey))
        {
            return Result.NotFound("The agent does not exist.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.Set<AiAgentSetting>().FirstOrDefaultAsync(a => a.AgentKey == agentKey, cancellationToken);
        if (setting is null)
        {
            setting = new AiAgentSetting { AgentKey = agentKey };
            db.Set<AiAgentSetting>().Add(setting);
        }

        setting.IsEnabled = enabled;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            new AuditEntry("config", enabled ? "ai.agent.enabled" : "ai.agent.disabled") { TargetType = "ai_agent", TargetName = agentKey, Message = $"AI agent '{agentKey}' {(enabled ? "enabled" : "disabled")}." },
            cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<AiUsageStats>> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<AiUsageStats>.Forbidden();
        }

        var today = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var since = today.AddDays(-6);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var conversations = await db.Set<AiConversation>().CountAsync(cancellationToken);
        var messages = await db.Set<AiMessage>().AsNoTracking().Where(m => m.CreatedAt >= since)
            .OrderByDescending(m => m.CreatedAt).Take(50_000)
            .Select(m => new { m.CreatedAt, m.InputTokens, m.OutputTokens }).ToListAsync(cancellationToken);
        var toolCalls = await db.Set<AiToolCall>().CountAsync(t => t.CreatedAt >= since, cancellationToken);
        var pending = await db.Set<AiToolCall>().CountAsync(t => t.Status == AiToolCallStatus.PendingConfirmation, cancellationToken);
        var daily = Enumerable.Range(0, 7)
            .Select(i => (double)messages.Count(m => m.CreatedAt >= since.AddDays(i) && m.CreatedAt < since.AddDays(i + 1)))
            .ToList();
        var tokens = messages.Sum(m => (long)(m.InputTokens ?? 0) + (m.OutputTokens ?? 0));
        return Result<AiUsageStats>.Success(new AiUsageStats(conversations, messages.Count, toolCalls, pending, tokens, daily));
    }

    /// <summary>Validates a provider configuration.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The validation result.</returns>
    internal static Result Validate(AiProviderSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            return Result.Invalid("Enter a name with at most 100 characters.");
        }

        if (!Enum.IsDefined(request.Kind))
        {
            return Result.Invalid("Select a provider type.");
        }

        if (!Uri.TryCreate(request.BaseUrl?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || request.BaseUrl!.Length > 500)
        {
            return Result.Invalid("The base URL must be an absolute http(s) URL without credentials, query or fragment.");
        }

        if (request.Kind == AiProviderKind.AzureOpenAi)
        {
            if (string.IsNullOrWhiteSpace(request.Deployment) || request.Deployment.Length > 200)
            {
                return Result.Invalid("Enter the Azure OpenAI deployment name.");
            }
        }
        else if (string.IsNullOrWhiteSpace(request.Model) || request.Model.Length > 200)
        {
            return Result.Invalid("Enter the model name.");
        }

        if (request.MaxTokens is < 16 or > 32_000)
        {
            return Result.Invalid("Max tokens must be between 16 and 32,000.");
        }

        if (request.Temperature is < 0 or > 2 || double.IsNaN(request.Temperature))
        {
            return Result.Invalid("The temperature must be between 0 and 2.");
        }

        if (request.TimeoutSeconds is < 5 or > 600)
        {
            return Result.Invalid("The timeout must be between 5 and 600 seconds.");
        }

        if (request.ApiKey is { Length: > 4000 })
        {
            return Result.Invalid("The API key is too long.");
        }

        return Result.Success();
    }

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) => currentUser.HasPermissionAsync(Permissions.Ai.Manage, cancellationToken);
}
