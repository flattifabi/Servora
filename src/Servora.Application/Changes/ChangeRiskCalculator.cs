using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>
/// Simple, transparent risk questionnaire. Each answer contributes points; the total maps to a risk level:
/// 0–3 low, 4–7 moderate, 8–11 high, 12+ very high.
/// </summary>
public static class ChangeRiskCalculator
{
    /// <summary>Gets the questionnaire.</summary>
    public static IReadOnlyList<RiskQuestion> Questions { get; } =
    [
        new("users", "How many users are affected if the change fails?",
            [new("Fewer than 10", 0), new("10 – 100", 1), new("100 – 1,000", 2), new("More than 1,000", 3)]),
        new("outage", "Is a service outage required during implementation?",
            [new("No outage", 0), new("Partial / degraded service", 1), new("Full outage", 3)]),
        new("tested", "Was the change tested in a non-production environment?",
            [new("Yes, fully tested", 0), new("Partially tested", 1), new("Not tested", 3)]),
        new("backout", "Is there a tested backout plan?",
            [new("Yes, tested", 0), new("Yes, but untested", 1), new("No backout possible", 3)]),
        new("complexity", "How complex is the implementation?",
            [new("Routine, single team", 0), new("Moderate", 1), new("Complex, multiple teams", 2)]),
        new("experience", "Was a similar change implemented successfully before?",
            [new("Yes", 0), new("No", 1)]),
    ];

    /// <summary>Calculates the risk of a complete set of answers.</summary>
    /// <param name="answers">The answers (question key to option index).</param>
    /// <returns>The result, or <see langword="null"/> when a question is unanswered or an answer is out of range.</returns>
    public static RiskAssessmentResult? Calculate(IReadOnlyDictionary<string, int> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        var score = 0;
        foreach (var question in Questions)
        {
            if (!answers.TryGetValue(question.Key, out var index) || index < 0 || index >= question.Options.Count)
            {
                return null;
            }

            score += question.Options[index].Score;
        }

        return new RiskAssessmentResult(score, RiskFor(score));
    }

    /// <summary>Maps a score to a risk level.</summary>
    /// <param name="score">The score.</param>
    /// <returns>The risk level.</returns>
    public static ChangeRisk RiskFor(int score) => score switch
    {
        <= 3 => ChangeRisk.Low,
        <= 7 => ChangeRisk.Moderate,
        <= 11 => ChangeRisk.High,
        _ => ChangeRisk.VeryHigh,
    };
}
