namespace Servora.Application.Ai;

/// <summary>Assistant usage KPIs for the admin page.</summary>
/// <param name="Conversations">Total conversations.</param>
/// <param name="Messages7d">Messages in the last 7 days.</param>
/// <param name="ToolCalls7d">Trace steps in the last 7 days.</param>
/// <param name="PendingActions">Actions waiting for user confirmation.</param>
/// <param name="Tokens7d">Tokens (input + output) in the last 7 days.</param>
/// <param name="DailyMessages">Messages per day for the last 7 days, oldest first.</param>
public sealed record AiUsageStats(int Conversations, int Messages7d, int ToolCalls7d, int PendingActions, long Tokens7d, IReadOnlyList<double> DailyMessages);
