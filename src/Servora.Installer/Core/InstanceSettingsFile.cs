using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Reads and writes the instance configuration <c>&lt;data dir&gt;\appsettings.json</c>, which the host loads after
/// the shipped <c>appsettings.json</c>. The installer only ever sets <c>Urls</c>; every other key is preserved.
/// </summary>
/// <param name="fileSystem">The file system.</param>
internal sealed class InstanceSettingsFile(IFileSystem fileSystem)
{
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Builds the <c>Urls</c> value for a port (all interfaces, plain HTTP).</summary>
    /// <param name="port">The port.</param>
    /// <returns>The URL list, for example <c>http://0.0.0.0:5080</c>.</returns>
    public static string UrlsForPort(int port) => $"http://0.0.0.0:{port}";

    /// <summary>Sets <c>Urls</c> in the instance settings, creating the file when missing.</summary>
    /// <param name="path">The instance settings path.</param>
    /// <param name="urls">The URL list.</param>
    /// <exception cref="InstallerException">The existing file is not valid JSON.</exception>
    public void SetUrls(string path, string urls)
    {
        var root = fileSystem.FileExists(path) ? Parse(path) : new JsonObject();

        // Configuration keys are case-insensitive; a second "urls" key would make the host fail with a duplicate key.
        foreach (var key in root.Select(p => p.Key).Where(k => string.Equals(k, "Urls", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            root.Remove(key);
        }

        root["Urls"] = urls;

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            root.WriteTo(writer);
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            fileSystem.CreateDirectory(directory);
        }

        fileSystem.WriteAllText(path, Encoding.UTF8.GetString(buffer.ToArray()) + Environment.NewLine);
    }

    /// <summary>
    /// Reads the listen URLs from <c>Urls</c> (semicolon separated) and <c>Kestrel:Endpoints:*:Url</c>.
    /// Returns an empty list when the file is missing or invalid.
    /// </summary>
    /// <param name="path">The settings file path.</param>
    /// <returns>The configured URLs.</returns>
    public IReadOnlyList<string> ReadUrls(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return [];
        }

        JsonObject root;
        try
        {
            root = Parse(path);
        }
        catch (InstallerException)
        {
            return [];
        }

        var urls = new List<string>();
        if (TryGetString(root, "Urls") is { } list)
        {
            urls.AddRange(list.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (GetChild(root, "Kestrel") is JsonObject kestrel && GetChild(kestrel, "Endpoints") is JsonObject endpoints)
        {
            foreach (var endpoint in endpoints)
            {
                if (endpoint.Value is JsonObject endpointObject && TryGetString(endpointObject, "Url") is { } url)
                {
                    urls.Add(url.Trim());
                }
            }
        }

        return urls;
    }

    private JsonObject Parse(string path)
    {
        try
        {
            return JsonNode.Parse(fileSystem.ReadAllText(path), documentOptions: ReadOptions) as JsonObject
                ?? throw new InstallerException($"'{path}' must contain a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new InstallerException($"'{path}' is not valid JSON ({ex.Message}). Fix or remove the file and retry.", ex);
        }
    }

    private static JsonNode? GetChild(JsonObject parent, string name)
    {
        // Configuration keys are case-insensitive.
        foreach (var property in parent)
        {
            if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? TryGetString(JsonObject parent, string name) =>
        GetChild(parent, name) is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
