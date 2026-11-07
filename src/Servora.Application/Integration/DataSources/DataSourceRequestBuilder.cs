using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Servora.Application.Automation.Model;
using Servora.Application.Common;
using Servora.Domain.Integration;

namespace Servora.Application.Integration.DataSources;

/// <summary>
/// Validates parameters against an endpoint definition and builds the outgoing request. Guarantees that the final URI
/// keeps the data source's scheme, host, port and base path (no parameter can change the target), that path values are
/// encoded and contain no dot segments, and that JSON body values are JSON-encoded.
/// </summary>
public static partial class DataSourceRequestBuilder
{
    /// <summary>The HTTP methods an endpoint may use.</summary>
    public static readonly IReadOnlyList<string> AllowedMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    private static readonly TimeSpan s_patternTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Validates a base URL.</summary>
    /// <param name="baseUrl">The URL.</param>
    /// <param name="allowHttp">Whether plain HTTP is allowed.</param>
    /// <param name="uri">The parsed URI.</param>
    /// <returns>An error message or <see langword="null"/> when valid.</returns>
    public static string? ValidateBaseUrl(string? baseUrl, bool allowHttp, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var parsed))
        {
            return "The base URL must be an absolute URL.";
        }

        if (parsed.Scheme != Uri.UriSchemeHttps && !(allowHttp && parsed.Scheme == Uri.UriSchemeHttp))
        {
            return allowHttp ? "Only http and https URLs are supported." : "The base URL must use HTTPS (enable 'Allow HTTP' only for trusted internal services).";
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            return "Credentials must not be part of the URL; configure authentication instead.";
        }

        if (!string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return "The base URL must not contain a query string or fragment; use endpoint parameters.";
        }

        if (string.IsNullOrEmpty(parsed.Host))
        {
            return "The base URL has no host.";
        }

        uri = parsed;
        return null;
    }

    /// <summary>Validates an endpoint path template.</summary>
    /// <param name="template">The template.</param>
    /// <returns>An error message or <see langword="null"/> when valid.</returns>
    public static string? ValidatePathTemplate(string? template)
    {
        var path = template ?? string.Empty;
        if (path.Length > 1000)
        {
            return "The path template is too long.";
        }

        if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') || path.Contains('#'))
        {
            return "The path template must be a relative path (no scheme, host, backslashes or fragments).";
        }

        var pathPart = path.Split('?', 2)[0];
        if (pathPart.Split('/').Any(segment => segment is "." or ".."))
        {
            return "The path template must not contain dot segments.";
        }

        if (path.Any(char.IsControl) || path.Contains(' '))
        {
            return "The path template must not contain spaces or control characters.";
        }

        return null;
    }

    /// <summary>Parses the parameter definitions of an endpoint.</summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The definitions.</returns>
    public static List<EndpointParameterDefinition> ParseParameters(string? json) =>
        FlowJson.TryDeserialize<List<EndpointParameterDefinition>>(json) ?? [];

    /// <summary>Checks whether a parameter name is valid.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidParameterName(string? name) => name is not null && ParameterNameRegex().IsMatch(name);

    /// <summary>Builds the request for an endpoint.</summary>
    /// <param name="source">The data source.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="parameters">The supplied parameter values.</param>
    /// <returns>The request plan or a validation failure.</returns>
    public static Result<DataSourceRequestPlan> Build(DataSource source, DataSourceEndpoint endpoint, IReadOnlyDictionary<string, string>? parameters)
    {
        if (ValidateBaseUrl(source.BaseUrl, source.AllowHttp, out var baseUri) is { } baseError)
        {
            return Result<DataSourceRequestPlan>.Invalid(baseError);
        }

        if (ValidatePathTemplate(endpoint.PathTemplate) is { } pathError)
        {
            return Result<DataSourceRequestPlan>.Invalid(pathError);
        }

        var method = endpoint.Method.ToUpperInvariant();
        if (!AllowedMethods.Contains(method))
        {
            return Result<DataSourceRequestPlan>.Invalid($"The HTTP method '{endpoint.Method}' is not allowed.");
        }

        var definitions = ParseParameters(endpoint.ParametersJson);
        var supplied = parameters ?? new Dictionary<string, string>();
        foreach (var name in supplied.Keys)
        {
            if (!definitions.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return Result<DataSourceRequestPlan>.Invalid($"The parameter '{name}' is not allowed for this endpoint.");
            }
        }

        var values = new Dictionary<string, (EndpointParameterDefinition Definition, string Value)>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            var value = supplied.FirstOrDefault(p => string.Equals(p.Key, definition.Name, StringComparison.OrdinalIgnoreCase)).Value;
            if (string.IsNullOrEmpty(value))
            {
                value = definition.DefaultValue;
            }

            if (string.IsNullOrEmpty(value))
            {
                if (definition.Required)
                {
                    return Result<DataSourceRequestPlan>.Invalid($"The parameter '{definition.Name}' is required.");
                }

                continue;
            }

            if (ValidateValue(definition, value) is { } valueError)
            {
                return Result<DataSourceRequestPlan>.Invalid(valueError);
            }

            values[definition.Name] = (definition, value.Trim());
        }

        // Path
        var template = endpoint.PathTemplate ?? string.Empty;
        var templateParts = template.Split('?', 2);
        var pathBuilder = new StringBuilder();
        var pathTemplate = templateParts[0];
        var position = 0;
        foreach (Match match in PathPlaceholderRegex().Matches(pathTemplate))
        {
            pathBuilder.Append(pathTemplate, position, match.Index - position);
            var name = match.Groups["name"].Value;
            if (!values.TryGetValue(name, out var entry) || entry.Definition.Location != EndpointParameterLocation.Path)
            {
                return Result<DataSourceRequestPlan>.Invalid($"The path parameter '{name}' has no value.");
            }

            if (entry.Value is "." or "..")
            {
                return Result<DataSourceRequestPlan>.Invalid($"The value of path parameter '{name}' is not allowed.");
            }

            pathBuilder.Append(Uri.EscapeDataString(entry.Value));
            position = match.Index + match.Length;
        }

        pathBuilder.Append(pathTemplate, position, pathTemplate.Length - position);
        var relativePath = pathBuilder.ToString();
        if (relativePath.Contains('{') || relativePath.Contains('}'))
        {
            return Result<DataSourceRequestPlan>.Invalid("The path template contains an invalid placeholder.");
        }

        // Query
        var query = new StringBuilder(templateParts.Length > 1 ? templateParts[1] : string.Empty);
        foreach (var (definition, value) in values.Values.Where(v => v.Definition.Location == EndpointParameterLocation.Query))
        {
            if (query.Length > 0)
            {
                query.Append('&');
            }

            query.Append(Uri.EscapeDataString(definition.Name)).Append('=').Append(Uri.EscapeDataString(value));
        }

        var basePath = baseUri!.AbsolutePath.TrimEnd('/');
        var target = $"{baseUri.Scheme}://{baseUri.Authority}{basePath}/{relativePath.TrimStart('/')}{(query.Length > 0 ? "?" + query : string.Empty)}";
        if (!Uri.TryCreate(target, UriKind.Absolute, out var finalUri))
        {
            return Result<DataSourceRequestPlan>.Invalid("The request URL is invalid.");
        }

        if (finalUri.Scheme != baseUri.Scheme || !string.Equals(finalUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) || finalUri.Port != baseUri.Port
            || !finalUri.AbsolutePath.StartsWith(basePath, StringComparison.Ordinal))
        {
            return Result<DataSourceRequestPlan>.Invalid("The request would leave the data source's base URL.");
        }

        // Body
        string? body = null;
        if (method is "POST" or "PUT" or "PATCH" && !string.IsNullOrWhiteSpace(endpoint.BodyTemplate))
        {
            var bodyResult = RenderBody(endpoint.BodyTemplate, values);
            if (!bodyResult.Succeeded)
            {
                return Result<DataSourceRequestPlan>.From(bodyResult);
            }

            body = bodyResult.Value;
        }

        return Result<DataSourceRequestPlan>.Success(new DataSourceRequestPlan(method, finalUri, body));
    }

    /// <summary>Validates a single value against its definition.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="value">The value.</param>
    /// <returns>An error message or <see langword="null"/>.</returns>
    public static string? ValidateValue(EndpointParameterDefinition definition, string value)
    {
        var maxLength = Math.Clamp(definition.MaxLength <= 0 ? 200 : definition.MaxLength, 1, 2000);
        if (value.Length > maxLength)
        {
            return $"The parameter '{definition.Name}' exceeds {maxLength} characters.";
        }

        if (value.Any(char.IsControl))
        {
            return $"The parameter '{definition.Name}' contains control characters.";
        }

        switch (definition.Type)
        {
            case EndpointParameterType.Integer when !long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _):
                return $"The parameter '{definition.Name}' must be a whole number.";
            case EndpointParameterType.Number when !decimal.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _):
                return $"The parameter '{definition.Name}' must be a number.";
            case EndpointParameterType.Boolean when !bool.TryParse(value.Trim(), out _):
                return $"The parameter '{definition.Name}' must be true or false.";
            case EndpointParameterType.Enum when !definition.AllowedValues.Contains(value.Trim(), StringComparer.Ordinal):
                return $"The parameter '{definition.Name}' must be one of: {string.Join(", ", definition.AllowedValues)}.";
        }

        if (!string.IsNullOrWhiteSpace(definition.Pattern))
        {
            try
            {
                if (!Regex.IsMatch(value, $"^(?:{definition.Pattern})$", RegexOptions.CultureInvariant, s_patternTimeout))
                {
                    return $"The parameter '{definition.Name}' does not match the required format.";
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return $"The parameter '{definition.Name}' could not be validated.";
            }
            catch (ArgumentException)
            {
                return $"The validation pattern of parameter '{definition.Name}' is invalid.";
            }
        }

        return null;
    }

    private static Result<string> RenderBody(string template, Dictionary<string, (EndpointParameterDefinition Definition, string Value)> values)
    {
        var rendered = BodyPlaceholderRegex().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            if (!values.TryGetValue(name, out var entry))
            {
                return "null";
            }

            return entry.Definition.Type is EndpointParameterType.Integer or EndpointParameterType.Number or EndpointParameterType.Boolean
                ? entry.Value.ToLowerInvariant()
                : JsonEncodedText.Encode(entry.Value).ToString();
        });

        try
        {
            using var _ = JsonDocument.Parse(rendered, new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException)
        {
            return Result<string>.Invalid("The body template does not produce valid JSON.");
        }

        return Result<string>.Success(rendered);
    }

    [GeneratedRegex(@"\{(?<name>[A-Za-z0-9_.\-]{1,64})\}", RegexOptions.CultureInvariant)]
    private static partial Regex PathPlaceholderRegex();

    [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z0-9_.\-]{1,64})\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex BodyPlaceholderRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterNameRegex();
}
