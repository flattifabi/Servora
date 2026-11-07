namespace Servora.Application.Configuration;

/// <summary>
/// Contributes setting definitions to <see cref="ISystemSettingsService"/>. Modules register additional providers
/// (for example authentication policy or logging settings) with <c>services.AddSingleton&lt;ISettingDefinitionProvider, ...&gt;()</c>.
/// </summary>
public interface ISettingDefinitionProvider
{
    /// <summary>Returns the definitions of this provider.</summary>
    /// <returns>The definitions.</returns>
    IEnumerable<SettingDefinition> GetDefinitions();
}
