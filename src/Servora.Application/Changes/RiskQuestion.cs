namespace Servora.Application.Changes;

/// <summary>A question of the change risk assessment.</summary>
/// <param name="Key">The stable key used to store the answer.</param>
/// <param name="Text">The question text.</param>
/// <param name="Options">The answer options; the answer is stored as the option index.</param>
public sealed record RiskQuestion(string Key, string Text, IReadOnlyList<RiskOption> Options);
