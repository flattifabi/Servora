namespace Servora.Application.Configuration;

/// <summary>Immutable cached copy of a stored <c>sys_properties</c> row.</summary>
/// <param name="Key">The key.</param>
/// <param name="Value">The stored value.</param>
/// <param name="Category">The category.</param>
/// <param name="Description">The description.</param>
/// <param name="UpdatedAt">The last modification time.</param>
/// <param name="UpdatedBy">The last modifier.</param>
internal sealed record StoredSettingValue(string Key, string Value, string Category, string? Description, DateTimeOffset UpdatedAt, string? UpdatedBy);
