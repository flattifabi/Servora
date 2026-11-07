using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Ai;
using Servora.Domain.Observability;

namespace Servora.Application.Ai;

/// <summary>Default <see cref="IAiAssistantService"/>: persists conversations, messages and the execution trace.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="resolver">The connection resolver.</param>
/// <param name="orchestrator">The orchestrator.</param>
/// <param name="tools">The registered tools (titles for confirmation messages).</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class AiAssistantService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IAiConnectionResolver resolver,
    IAiOrchestrator orchestrator,
    IEnumerable<IMcpTool> tools,
    IAuditService audit,
    IClock clock) : IAiAssistantService
{
    /// <summary>The maximum length of a user message.</summary>
    public const int MaxMessageLength = 4000;

    private const int HistoryMessages = 12;

    /// <inheritdoc />
    public async Task<Result<AiAssistantStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanUseAsync(cancellationToken))
        {
            return Result<AiAssistantStatus>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var provider = await db.Set<AiProviderConfig>().AsNoTracking()
            .Where(p => p.IsEnabled).OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name)
            .Select(p => new { p.Name, p.Model, p.Deployment, p.Kind, p.SupportsTools })
            .FirstOrDefaultAsync(cancellationToken);
        var disabled = await DisabledAgentsAsync(db, cancellationToken);
        var toolList = tools.ToList();
        var agents = orchestrator.Agents.Where(a => !disabled.Contains(a.Key)).Select(a => new AiAgentInfo(
            a.Key,
            a.Name,
            a.Description,
            a.Icon,
            a.Scope,
            a.ToolNames.Select(n => new AiAgentToolInfo(n, toolList.FirstOrDefault(t => t.Name == n)?.Title ?? n, toolList.FirstOrDefault(t => t.Name == n)?.IsConsequential ?? false)).ToList(),
            true)).ToList();
        var canConfigure = await currentUser.HasPermissionAsync(Permissions.Ai.Manage, cancellationToken);
        return Result<AiAssistantStatus>.Success(new AiAssistantStatus(
            provider is not null,
            provider?.Name,
            provider is null ? null : provider.Kind == AiProviderKind.AzureOpenAi ? provider.Deployment : provider.Model,
            provider?.SupportsTools ?? false,
            canConfigure,
            agents));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<AiConversationSummary>>> ListConversationsAsync(string? search = null, int take = 50, CancellationToken cancellationToken = default)
    {
        if (!await CanUseAsync(cancellationToken) || currentUser.UserId is not { } userId)
        {
            return Result<IReadOnlyList<AiConversationSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<AiConversation>().AsNoTracking().Where(c => c.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Title.Contains(term));
        }

        IReadOnlyList<AiConversationSummary> list = await query
            .OrderByDescending(c => c.LastMessageAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(c => new AiConversationSummary(c.Id, c.Title, c.LastMessageAt, c.MessageCount))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<AiConversationSummary>>.Success(list);
    }

    /// <inheritdoc />
    public async Task<Result<AiConversationView>> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanUseAsync(cancellationToken) || currentUser.UserId is not { } userId)
        {
            return Result<AiConversationView>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var view = await LoadViewAsync(db, id, userId, cancellationToken);
        return view is null ? Result<AiConversationView>.NotFound("The conversation was not found.") : Result<AiConversationView>.Success(view);
    }

    /// <inheritdoc />
    public async Task<Result<AiConversationView>> SendAsync(Guid? conversationId, string message, CancellationToken cancellationToken = default)
    {
        if (!await CanUseAsync(cancellationToken) || currentUser.UserId is not { } userId)
        {
            return Result<AiConversationView>.Forbidden();
        }

        message = message?.Trim() ?? string.Empty;
        if (message.Length == 0 || message.Length > MaxMessageLength)
        {
            return Result<AiConversationView>.Invalid($"Enter a message with at most {MaxMessageLength:N0} characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        AiConversation? conversation;
        if (conversationId is { } existingId)
        {
            conversation = await db.Set<AiConversation>().FirstOrDefaultAsync(c => c.Id == existingId && c.UserId == userId, cancellationToken);
            if (conversation is null)
            {
                return Result<AiConversationView>.NotFound("The conversation was not found.");
            }
        }
        else
        {
            conversation = new AiConversation { UserId = userId, Title = BuildTitle(message) };
            db.Set<AiConversation>().Add(conversation);
        }

        List<AiChatMessage> history = conversationId is null
            ? []
            : (await db.Set<AiMessage>().AsNoTracking()
                .Where(m => m.ConversationId == conversation.Id && !m.IsError)
                .OrderByDescending(m => m.CreatedAt).Take(HistoryMessages)
                .Select(m => new { m.Role, m.Content })
                .ToListAsync(cancellationToken))
                .AsEnumerable().Reverse()
                .Select(m => m.Role == AiMessageRole.User ? AiChatMessage.User(McpJson.Truncate(m.Content, MaxMessageLength)) : AiChatMessage.Assistant(McpJson.Truncate(m.Content, MaxMessageLength)))
                .ToList();

        var now = clock.UtcNow;
        db.Set<AiMessage>().Add(new AiMessage { ConversationId = conversation.Id, Role = AiMessageRole.User, Content = message, CreatedAt = now });
        conversation.LastMessageAt = now;
        conversation.MessageCount++;
        await db.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var assistant = new AiMessage { ConversationId = conversation.Id, Role = AiMessageRole.Assistant };
        var connection = await resolver.GetDefaultAsync(cancellationToken);
        IReadOnlyList<AiTraceStep> steps = [];
        if (connection is null)
        {
            assistant.IsError = true;
            assistant.Content = "No AI provider is configured yet. An administrator can add one under **Administration › AI & Agents**.";
        }
        else
        {
            var run = await orchestrator.RunAsync(
                new AiOrchestrationRequest(userId, currentUser.DisplayName, history, message, connection)
                {
                    DisabledAgents = await DisabledAgentsAsync(db, cancellationToken),
                    CorrelationId = currentUser.CorrelationId,
                },
                cancellationToken);
            if (run.Succeeded)
            {
                var value = run.Value!;
                assistant.Content = string.IsNullOrWhiteSpace(value.Answer) ? "_(The model returned an empty answer.)_" : McpJson.Truncate(value.Answer, 31_000);
                assistant.AgentKey = value.AgentKey;
                assistant.InputTokens = value.InputTokens;
                assistant.OutputTokens = value.OutputTokens;
                steps = value.Steps;
            }
            else
            {
                assistant.IsError = true;
                assistant.Content = $"The AI provider could not answer: {run.Error}";
            }
        }

        assistant.CreatedAt = clock.UtcNow;
        assistant.DurationMs = stopwatch.ElapsedMilliseconds;
        db.Set<AiMessage>().Add(assistant);
        var sequence = 0;
        foreach (var step in steps.Take(100))
        {
            db.Set<AiToolCall>().Add(new AiToolCall
            {
                ConversationId = conversation.Id,
                MessageId = assistant.Id,
                Sequence = sequence++,
                Kind = step.Kind,
                AgentKey = step.AgentKey,
                ToolName = step.ToolName,
                ArgumentsJson = step.ArgumentsJson is null ? null : McpJson.Truncate(step.ArgumentsJson, 7900),
                ResultJson = step.ResultText is null ? null : McpJson.Truncate(step.ResultText, 15_900),
                Status = step.Status,
                ErrorMessage = step.Error is null ? null : McpJson.Truncate(step.Error, 990),
                ActorName = step.ActorName is null ? null : McpJson.Truncate(step.ActorName, 250),
                DurationMs = step.DurationMs,
                CreatedAt = assistant.CreatedAt,
            });
        }

        conversation.LastMessageAt = assistant.CreatedAt;
        conversation.MessageCount++;
        await db.SaveChangesAsync(cancellationToken);

        var view = await LoadViewAsync(db, conversation.Id, userId, cancellationToken);
        return Result<AiConversationView>.Success(view!);
    }

    /// <inheritdoc />
    public async Task<Result<AiConversationView>> ConfirmActionAsync(Guid toolCallId, CancellationToken cancellationToken = default)
    {
        if (!await CanUseAsync(cancellationToken) || currentUser.UserId is not { } userId)
        {
            return Result<AiConversationView>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (step, conversation) = await LoadPendingAsync(db, toolCallId, userId, cancellationToken);
        if (step is null || conversation is null)
        {
            return Result<AiConversationView>.NotFound("The action was not found or was already decided.");
        }

        // Claim the action atomically so that a double click (or two tabs) can never execute it twice.
        if (!await TryClaimAsync(db, step.Id, AiToolCallStatus.Confirmed, cancellationToken))
        {
            return Result<AiConversationView>.NotFound("The action was not found or was already decided.");
        }

        var stopwatch = Stopwatch.StartNew();
        var executed = await orchestrator.ExecuteConfirmedActionAsync(
            new AiActionRequest(userId, currentUser.DisplayName, step.AgentKey, step.ToolName, step.ArgumentsJson ?? "{}") { CorrelationId = currentUser.CorrelationId },
            cancellationToken);

        var now = clock.UtcNow;
        step.DecidedAt = now;
        step.DurationMs = stopwatch.ElapsedMilliseconds;
        var title = tools.FirstOrDefault(t => t.Name == step.ToolName)?.Title ?? step.ToolName;
        string content;
        if (executed.Succeeded)
        {
            step.Status = AiToolCallStatus.Confirmed;
            step.ResultJson = McpJson.Truncate(executed.Value!, 15_900);
            var detail = McpJson.TryParse(executed.Value, out var node) && node is JsonObject o && McpJson.GetString(o, "message") is { } m ? m : "The action completed.";
            content = $"**{title}** confirmed and executed. {detail}";
        }
        else
        {
            step.Status = executed.ErrorKind == ResultErrorKind.Forbidden ? AiToolCallStatus.Denied : AiToolCallStatus.Failed;
            step.ErrorMessage = McpJson.Truncate(executed.Error ?? "Failed.", 990);
            content = $"**{title}** could not be executed: {executed.Error}";
        }

        db.Set<AiMessage>().Add(new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiMessageRole.Assistant,
            Content = content,
            AgentKey = step.AgentKey,
            IsError = !executed.Succeeded,
            CreatedAt = now,
            DurationMs = stopwatch.ElapsedMilliseconds,
        });
        conversation.LastMessageAt = now;
        conversation.MessageCount++;
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            new AuditEntry("ai", "action.confirmed", executed.Succeeded ? AuditOutcome.Success : AuditOutcome.Failure)
            {
                TargetType = "ai_tool_call",
                TargetId = step.Id.ToString(),
                TargetName = step.ToolName,
                Message = executed.Succeeded ? $"AI action '{step.ToolName}' confirmed and executed." : $"AI action '{step.ToolName}' failed: {executed.Error}",
                Details = new { agent = step.AgentKey, conversationId = conversation.Id },
            },
            cancellationToken);

        return Result<AiConversationView>.Success((await LoadViewAsync(db, conversation.Id, userId, cancellationToken))!);
    }

    /// <inheritdoc />
    public async Task<Result<AiConversationView>> RejectActionAsync(Guid toolCallId, CancellationToken cancellationToken = default)
    {
        if (!await CanUseAsync(cancellationToken) || currentUser.UserId is not { } userId)
        {
            return Result<AiConversationView>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (step, conversation) = await LoadPendingAsync(db, toolCallId, userId, cancellationToken);
        if (step is null || conversation is null)
        {
            return Result<AiConversationView>.NotFound("The action was not found or was already decided.");
        }

        if (!await TryClaimAsync(db, step.Id, AiToolCallStatus.Rejected, cancellationToken))
        {
            return Result<AiConversationView>.NotFound("The action was not found or was already decided.");
        }

        step.Status = AiToolCallStatus.Rejected;
        step.DecidedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            new AuditEntry("ai", "action.rejected") { TargetType = "ai_tool_call", TargetId = step.Id.ToString(), TargetName = step.ToolName, Message = $"AI action '{step.ToolName}' rejected by the user." },
            cancellationToken);
        return Result<AiConversationView>.Success((await LoadViewAsync(db, conversation.Id, userId, cancellationToken))!);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanUseAsync(cancellationToken) || currentUser.UserId is not { } userId)
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await db.Set<AiConversation>().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (conversation is null)
        {
            return Result.NotFound("The conversation was not found.");
        }

        await db.Set<AiToolCall>().Where(t => t.ConversationId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<AiMessage>().Where(m => m.ConversationId == id).ExecuteDeleteAsync(cancellationToken);
        db.Set<AiConversation>().Remove(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Builds a conversation title from the first message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The title (max 80 characters, single line).</returns>
    internal static string BuildTitle(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return line.Length <= 80 ? line : line[..77].TrimEnd() + "…";
    }

    private static async Task<IReadOnlySet<string>> DisabledAgentsAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        (await db.Set<AiAgentSetting>().AsNoTracking().Where(a => !a.IsEnabled).Select(a => a.AgentKey).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

    private static async Task<bool> TryClaimAsync(IAppDbContext db, Guid toolCallId, AiToolCallStatus decision, CancellationToken cancellationToken) =>
        await db.Set<AiToolCall>()
            .Where(t => t.Id == toolCallId && t.Status == AiToolCallStatus.PendingConfirmation)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, decision), cancellationToken) == 1;

    private static async Task<(AiToolCall? Step, AiConversation? Conversation)> LoadPendingAsync(IAppDbContext db, Guid toolCallId, Guid userId, CancellationToken cancellationToken)
    {
        var step = await db.Set<AiToolCall>().FirstOrDefaultAsync(t => t.Id == toolCallId && t.Status == AiToolCallStatus.PendingConfirmation, cancellationToken);
        if (step is null)
        {
            return (null, null);
        }

        var conversation = await db.Set<AiConversation>().FirstOrDefaultAsync(c => c.Id == step.ConversationId && c.UserId == userId, cancellationToken);
        return conversation is null ? (null, null) : (step, conversation);
    }

    private async Task<AiConversationView?> LoadViewAsync(IAppDbContext db, Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        var conversation = await db.Set<AiConversation>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        var messages = (await db.Set<AiMessage>().AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt).Take(200)
            .ToListAsync(cancellationToken)).OrderBy(m => m.CreatedAt).ThenBy(m => m.Role).ToList();
        var messageIds = messages.Select(m => m.Id).ToList();
        var steps = await db.Set<AiToolCall>().AsNoTracking().Where(t => messageIds.Contains(t.MessageId)).ToListAsync(cancellationToken);
        var names = orchestrator.Agents.ToDictionary(a => a.Key, a => a.Name, StringComparer.Ordinal);
        names[AiOrchestrator.OrchestratorKey] = "Orchestrator";

        var views = messages.Select(m => new AiMessageView(
            m.Id,
            m.Role,
            m.Content,
            m.AgentKey,
            m.AgentKey is null ? null : names.GetValueOrDefault(m.AgentKey, m.AgentKey),
            m.IsError,
            m.CreatedAt,
            m.DurationMs,
            steps.Where(s => s.MessageId == m.Id).OrderBy(s => s.Sequence).Select(s => new AiToolCallView(
                s.Id,
                s.Kind,
                s.AgentKey,
                names.GetValueOrDefault(s.AgentKey, s.AgentKey),
                s.ToolName,
                s.ArgumentsJson,
                s.ResultJson,
                s.Status,
                s.ErrorMessage,
                s.ActorName,
                s.DurationMs)).ToList())).ToList();
        return new AiConversationView(conversation.Id, conversation.Title, views);
    }

    private Task<bool> CanUseAsync(CancellationToken cancellationToken) => currentUser.HasPermissionAsync(Permissions.Ai.UseAssistant, cancellationToken);
}
