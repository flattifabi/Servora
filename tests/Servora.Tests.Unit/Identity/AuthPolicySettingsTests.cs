using Servora.Application.Identity;
using Servora.Application.Identity.Policies;

namespace Servora.Tests.Unit.Identity;

/// <summary>Range validation and password policy conversion of <see cref="AuthPolicySettings"/>, plus generated temporary passwords.</summary>
public sealed class AuthPolicySettingsTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        Assert.Null(new AuthPolicySettings().Validate());
    }

    [Theory]
    [InlineData(7, 5, 15, 480, 14)]
    [InlineData(129, 5, 15, 480, 14)]
    [InlineData(10, 0, 15, 480, 14)]
    [InlineData(10, 5, 0, 480, 14)]
    [InlineData(10, 5, 15, 4, 14)]
    [InlineData(10, 5, 15, 480, 91)]
    public void OutOfRangeValues_AreRejected(int length, int attempts, int lockout, int session, int rememberDays)
    {
        var settings = new AuthPolicySettings
        {
            MinimumPasswordLength = length,
            MaxFailedAttempts = attempts,
            LockoutMinutes = lockout,
            SessionLifetimeMinutes = session,
            RememberMeDays = rememberDays,
        };

        Assert.NotNull(settings.Validate());
    }

    [Fact]
    public void ToPasswordPolicy_CarriesRulesAndLockout()
    {
        var policy = new AuthPolicySettings { MinimumPasswordLength = 14, RequireSymbol = true, MaxFailedAttempts = 3, LockoutMinutes = 30 }.ToPasswordPolicy();

        Assert.Equal(14, policy.MinimumLength);
        Assert.True(policy.RequireSymbol);
        Assert.Equal(3, policy.MaxFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(30), policy.LockoutDuration);
        Assert.NotNull(policy.Validate("Abcdefghijkl12", "jane"));
        Assert.Null(policy.Validate("Abcdefghijkl12!", "jane"));
        Assert.Contains("a symbol", policy.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void TemporaryPassword_SatisfiesStrictestPolicy_AndIsRandom()
    {
        var strict = new PasswordPolicy { MinimumLength = 32, RequireDigit = true, RequireMixedCase = true, RequireSymbol = true };

        var first = TemporaryPassword.Generate(32);
        var second = TemporaryPassword.Generate(32);

        Assert.Equal(32, first.Length);
        Assert.Null(strict.Validate(first, "jane"));
        Assert.NotEqual(first, second);
        Assert.Equal(16, TemporaryPassword.Generate(4).Length);
    }
}
