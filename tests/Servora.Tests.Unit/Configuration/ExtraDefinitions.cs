using Servora.Application.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>A module-style definition provider used to verify that providers are merged.</summary>
internal sealed class ExtraDefinitions : ISettingDefinitionProvider
{
    /// <inheritdoc />
    public IEnumerable<SettingDefinition> GetDefinitions() =>
    [
        new("identity.lockout_threshold", SettingCategories.Security, "Lockout threshold", SettingValueType.Integer, "5") { Min = 1, Max = 50 },
    ];
}
