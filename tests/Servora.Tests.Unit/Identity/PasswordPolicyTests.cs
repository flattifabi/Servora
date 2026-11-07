using Servora.Application.Identity;

namespace Servora.Tests.Unit.Identity;

/// <summary>Tests for <see cref="PasswordPolicy"/>.</summary>
public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("NoDigitsHereAtAll")]
    [InlineData("admin")]
    public void Validate_RejectsWeakPasswords(string password)
    {
        Assert.NotNull(PasswordPolicy.Default.Validate(password));
    }

    [Fact]
    public void Validate_RejectsPasswordContainingUserName()
    {
        Assert.NotNull(PasswordPolicy.Default.Validate("Jane.Doe-2026!", "jane.doe"));
    }

    [Fact]
    public void Validate_AcceptsStrongPassword()
    {
        Assert.Null(PasswordPolicy.Default.Validate("Correct-Horse-42", "admin"));
    }
}
