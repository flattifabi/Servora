using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>Outcome of a risk assessment.</summary>
/// <param name="Score">The total risk score.</param>
/// <param name="Risk">The resulting risk level.</param>
public sealed record RiskAssessmentResult(int Score, ChangeRisk Risk);
