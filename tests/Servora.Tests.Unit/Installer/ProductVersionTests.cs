using Servora.Installer.Core;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="ProductVersion"/>.</summary>
public sealed class ProductVersionTests
{
    [Theory]
    [InlineData("1.2.0+5f3c1a9", "1.2.0")]
    [InlineData("1.3.0-beta.2+abc", "1.3.0-beta.2")]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    public void Display_StripsBuildMetadata(string? raw, string expected)
    {
        Assert.Equal(expected, ProductVersion.Display(raw));
    }

    [Theory]
    [InlineData("1.1.0", "1.0.0", 1)]
    [InlineData("1.0.0", "1.1.0", -1)]
    [InlineData("1.0.0+aaa", "1.0.0+bbb", 0)]
    [InlineData("1.0", "1.0.0.0", 0)]
    [InlineData("1.0.0", "1.0.0-rc.1", 1)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    [InlineData("1.0.0-beta", "1.0.0-beta.1", -1)]
    [InlineData("10.0.0", "9.9.9", 1)]
    public void Compare_FollowsSemanticVersionPrecedence(string left, string right, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(ProductVersion.Compare(left, right)!.Value));
    }

    [Fact]
    public void Compare_UnparsableVersion_ReturnsNull()
    {
        Assert.Null(ProductVersion.Compare("abc", "1.0.0"));
        Assert.Null(ProductVersion.Compare("1.0.0", null));
    }
}
