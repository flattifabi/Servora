using Servora.Application.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for the outbox back-off schedule.</summary>
public sealed class OutboxRetryPolicyTests
{
    private static readonly DateTimeOffset s_now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    [InlineData(5, 240)]
    [InlineData(9, 240)]
    public void Delay_GrowsExponentiallyAndCaps(int attempts, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), OutboxRetryPolicy.GetDelay(attempts));

    [Fact]
    public void NextAttempt_IsNull_WhenAttemptsAreExhausted()
    {
        Assert.Equal(s_now.AddMinutes(5), OutboxRetryPolicy.GetNextAttempt(2, 6, s_now));
        Assert.Null(OutboxRetryPolicy.GetNextAttempt(6, 6, s_now));
        Assert.Null(OutboxRetryPolicy.GetNextAttempt(1, 1, s_now));
        Assert.Null(OutboxRetryPolicy.GetNextAttempt(1, 0, s_now));
    }
}
