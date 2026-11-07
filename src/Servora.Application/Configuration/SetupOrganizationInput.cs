namespace Servora.Application.Configuration;

/// <summary>Organisation step of the setup wizard. Mutable so it can be bound to a form.</summary>
public sealed class SetupOrganizationInput
{
    /// <summary>Gets or sets the company name.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Gets or sets the product name.</summary>
    public string ProductName { get; set; } = CoreSettingDefinitionProvider.DefaultProductName;

    /// <summary>Gets or sets the accent color.</summary>
    public string AccentColor { get; set; } = CoreSettingDefinitionProvider.DefaultAccentColor;

    /// <summary>Gets or sets the default time zone.</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>Converts the input into setting values.</summary>
    /// <returns>The raw values by setting key.</returns>
    public IReadOnlyDictionary<string, string?> ToSettings() => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        [SystemSettingKeys.CompanyName] = CompanyName,
        [SystemSettingKeys.ProductName] = ProductName,
        [SystemSettingKeys.AccentColor] = AccentColor,
        [SystemSettingKeys.TimeZone] = TimeZone,
    };

    /// <summary>Validates the input with the setting definitions.</summary>
    /// <returns>The first validation error, or <see langword="null"/> when valid.</returns>
    public string? Validate()
    {
        foreach (var (key, value) in ToSettings())
        {
            var definition = CoreSettingDefinitionProvider.All.First(d => d.Key == key);
            var result = SettingValueParser.Normalize(definition, value);
            if (!result.Succeeded)
            {
                return result.Error;
            }
        }

        return null;
    }
}
