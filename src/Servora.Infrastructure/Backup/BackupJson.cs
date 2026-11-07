using System.Text.Json;
using System.Text.Json.Serialization;

namespace Servora.Infrastructure.Backup;

/// <summary>JSON settings shared by manifests, <c>backup.json</c> and restore request files.</summary>
public static class BackupJson
{
    /// <summary>Gets the serializer options (indented, camelCase, enums as strings).</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
