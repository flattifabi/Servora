namespace Servora.Application.Changes;

/// <summary>An answer option of a <see cref="RiskQuestion"/>.</summary>
/// <param name="Text">The option text.</param>
/// <param name="Score">The risk points contributed by the option.</param>
public sealed record RiskOption(string Text, int Score);
