using Servora.Application.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for setting typing, normalization and defaults.</summary>
public sealed class SettingValueParserTests
{
    private static SettingDefinition Def(SettingValueType type, string? defaultValue = null) => new("test.key", "General", "Test", type, defaultValue);

    [Theory]
    [InlineData("#7C6CFF", "#7c6cff")]
    [InlineData("#abc", "#aabbcc")]
    [InlineData("  #14b8a6 ", "#14b8a6")]
    public void Color_IsNormalizedToLowerCaseSixDigits(string input, string expected)
    {
        var result = SettingValueParser.Normalize(Def(SettingValueType.Color), input);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("7c6cff")]
    [InlineData("#12345")]
    [InlineData("red")]
    public void Color_RejectsInvalidValues(string input) =>
        Assert.False(SettingValueParser.Normalize(Def(SettingValueType.Color), input).Succeeded);

    [Theory]
    [InlineData("TRUE", "true")]
    [InlineData("yes", "true")]
    [InlineData("1", "true")]
    [InlineData("off", "false")]
    [InlineData("No", "false")]
    public void Boolean_AcceptsCommonSpellings(string input, string expected) =>
        Assert.Equal(expected, SettingValueParser.Normalize(Def(SettingValueType.Boolean, "false"), input).Value);

    [Fact]
    public void EmptyBooleanAndInteger_FallBackToDefault()
    {
        Assert.Equal("true", SettingValueParser.Normalize(Def(SettingValueType.Boolean, "true"), "  ").Value);
        Assert.Equal("25", SettingValueParser.Normalize(Def(SettingValueType.Integer, "25"), null).Value);
    }

    [Fact]
    public void Integer_EnforcesBounds()
    {
        var definition = Def(SettingValueType.Integer, "25") with { Min = 10, Max = 200 };

        Assert.False(SettingValueParser.Normalize(definition, "5").Succeeded);
        Assert.False(SettingValueParser.Normalize(definition, "201").Succeeded);
        Assert.False(SettingValueParser.Normalize(definition, "12.5").Succeeded);
        Assert.Equal("200", SettingValueParser.Normalize(definition, "+200").Value);
    }

    [Fact]
    public void Choice_ReturnsCanonicalSpelling()
    {
        var definition = Def(SettingValueType.Choice) with { Choices = ["StartTls", "None"] };

        Assert.Equal("StartTls", SettingValueParser.Normalize(definition, "starttls").Value);
        Assert.False(SettingValueParser.Normalize(definition, "Ssl").Succeeded);
    }

    [Theory]
    [InlineData("https://servora.example.com/", true)]
    [InlineData("http://localhost:5080", true)]
    [InlineData("ftp://servora.example.com", false)]
    [InlineData("https://user:pw@servora.example.com", false)]
    [InlineData("servora.example.com", false)]
    public void Url_AllowsOnlyHttpWithoutCredentials(string input, bool valid) =>
        Assert.Equal(valid, SettingValueParser.Normalize(Def(SettingValueType.Url), input).Succeeded);

    [Fact]
    public void Url_TrailingSlashIsRemoved() =>
        Assert.Equal("https://a.example.com", SettingValueParser.Normalize(Def(SettingValueType.Url), "https://a.example.com/").Value);

    [Theory]
    [InlineData("servicedesk@example.com", true)]
    [InlineData("Service Desk <servicedesk@example.com>", false)]
    [InlineData("not-an-address", false)]
    public void Email_AcceptsOnlyPlainAddresses(string input, bool valid) =>
        Assert.Equal(valid, SettingValueParser.Normalize(Def(SettingValueType.Email), input).Succeeded);

    [Fact]
    public void Required_RejectsEmptyAndLengthIsLimited()
    {
        var definition = Def(SettingValueType.String) with { Required = true, MaxLength = 5 };

        Assert.False(SettingValueParser.Normalize(definition, " ").Succeeded);
        Assert.False(SettingValueParser.Normalize(definition, "123456").Succeeded);
        Assert.True(SettingValueParser.Normalize(definition, "12345").Succeeded);
    }

    [Fact]
    public void SingleLineText_RejectsControlCharacters()
    {
        Assert.False(SettingValueParser.Normalize(Def(SettingValueType.String), "a\nb").Succeeded);
        Assert.True(SettingValueParser.Normalize(Def(SettingValueType.MultilineText), "a\r\nb").Succeeded);
    }

    [Fact]
    public void TimeZone_MustBeKnown()
    {
        Assert.True(SettingValueParser.Normalize(Def(SettingValueType.TimeZone), "UTC").Succeeded);
        Assert.False(SettingValueParser.Normalize(Def(SettingValueType.TimeZone), "Mars/Olympus").Succeeded);
    }

    [Fact]
    public void TryConvert_SupportsCommonTypes()
    {
        Assert.True(SettingValueParser.TryConvert<int>("42", out var number) && number == 42);
        Assert.True(SettingValueParser.TryConvert<bool>("true", out var flag) && flag);
        Assert.True(SettingValueParser.TryConvert<SmtpSecurityMode>("starttls", out var mode) && mode == SmtpSecurityMode.StartTls);
        Assert.False(SettingValueParser.TryConvert<int>("", out _));
        Assert.False(SettingValueParser.TryConvert<SmtpSecurityMode>("99", out _));
    }

    [Theory]
    [InlineData("features.new_dashboard", true)]
    [InlineData("features.X", false)]
    [InlineData("features.1abc", false)]
    [InlineData("general.base_url", false)]
    public void FeatureKeys_AreValidated(string key, bool valid) => Assert.Equal(valid, SettingValueParser.IsValidFeatureKey(key));

    [Fact]
    public void BuiltInDefinitions_HaveValidKeysAndDefaults()
    {
        foreach (var definition in CoreSettingDefinitionProvider.All)
        {
            Assert.True(SettingValueParser.IsValidKey(definition.Key), definition.Key);
            if (!string.IsNullOrEmpty(definition.DefaultValue))
            {
                var normalized = SettingValueParser.Normalize(definition, definition.DefaultValue);
                Assert.True(normalized.Succeeded, $"{definition.Key}: {normalized.Error}");
                Assert.Equal(definition.DefaultValue, normalized.Value);
            }
        }

        Assert.Equal(CoreSettingDefinitionProvider.All.Count, CoreSettingDefinitionProvider.All.Select(d => d.Key).Distinct().Count());
    }
}
