using Servora.Application.Tables;

namespace Servora.Application.Platform;

/// <summary>Builds UI routes of records.</summary>
public static class RecordUrls
{
    /// <summary>Gets the relative route of a record: the module route for typed tables, the generic form otherwise.</summary>
    /// <param name="table">The table.</param>
    /// <param name="id">The record identifier.</param>
    /// <returns>The relative route.</returns>
    public static string For(TableDefinition table, Guid id) =>
        table.IsCustom || string.IsNullOrWhiteSpace(table.Route) || !table.Route.StartsWith('/')
            ? $"/table/{Uri.EscapeDataString(table.Name)}/{id}"
            : $"{table.Route.TrimEnd('/')}/{id}";

    /// <summary>Makes a relative route absolute when a public URL is configured.</summary>
    /// <param name="publicUrl">The configured public URL (may be empty).</param>
    /// <param name="relative">The relative route.</param>
    /// <returns>The absolute URL or the relative route.</returns>
    public static string Absolute(string? publicUrl, string relative) =>
        Uri.TryCreate(publicUrl, UriKind.Absolute, out var baseUri) && (baseUri.Scheme == Uri.UriSchemeHttps || baseUri.Scheme == Uri.UriSchemeHttp)
            ? new Uri(baseUri, relative).ToString()
            : relative;
}
