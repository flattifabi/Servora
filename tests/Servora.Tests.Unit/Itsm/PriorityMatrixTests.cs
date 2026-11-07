using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.Itsm;

/// <summary>Tests for <see cref="PriorityMatrix"/>.</summary>
public sealed class PriorityMatrixTests
{
    [Theory]
    [InlineData(Impact.High, Urgency.High, Priority.Critical)]
    [InlineData(Impact.High, Urgency.Medium, Priority.High)]
    [InlineData(Impact.Medium, Urgency.Medium, Priority.Moderate)]
    [InlineData(Impact.Low, Urgency.Medium, Priority.Low)]
    [InlineData(Impact.Low, Urgency.Low, Priority.Planning)]
    public void Calculate_FollowsItilMatrix(Impact impact, Urgency urgency, Priority expected)
    {
        Assert.Equal(expected, PriorityMatrix.Calculate(impact, urgency));
    }
}
