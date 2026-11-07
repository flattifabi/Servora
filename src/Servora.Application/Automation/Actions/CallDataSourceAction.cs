using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Application.Integration;
using Servora.Application.Tables;

namespace Servora.Application.Automation.Actions;

/// <summary>Executes an approved data source endpoint through <see cref="IDataSourceExecutor"/> (SSRF-protected).</summary>
/// <param name="executor">The data source executor.</param>
public sealed class CallDataSourceAction(IDataSourceExecutor executor) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "datasource.call";

    /// <summary>The maximum body length kept in the step output.</summary>
    public const int MaxBodyLength = 100_000;

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Call data source", "Integration", "plug", "info", "Call an approved REST endpoint and use the JSON response.")
    {
        Fields =
        [
            new FlowActionField("dataSourceId", "Data source", FlowActionFieldKind.DataSource) { Required = true, SupportsPills = false },
            new FlowActionField("endpoint", "Endpoint", FlowActionFieldKind.DataSourceEndpoint) { Required = true, SupportsPills = false, TableFieldKey = "dataSourceId" },
            new FlowActionField("parameters", "Parameters", FlowActionFieldKind.KeyValues) { Help = "Values are validated against the endpoint's parameter rules." },
            new FlowActionField("failOnError", "Fail on HTTP error", FlowActionFieldKind.Select) { DefaultValue = "yes", SupportsPills = false, Options = [new FieldChoice("yes", "Yes (non-2xx fails the step)"), new FieldChoice("no", "No (continue with status code)")] },
        ],
        Outputs = [new FlowActionOutput("statusCode", "HTTP status"), new FlowActionOutput("json", "JSON response"), new FlowActionOutput("body", "Response text"), new FlowActionOutput("durationMs", "Duration (ms)")],
    };

    /// <inheritdoc />
    public async Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        if (FlowValues.ParseGuid(context.GetRaw("dataSourceId")) is not { } dataSourceId)
        {
            return FlowActionResult.Fail("No data source is configured.");
        }

        var endpoint = context.GetRaw("endpoint")?.Trim();
        if (string.IsNullOrEmpty(endpoint))
        {
            return FlowActionResult.Fail("No endpoint is configured.");
        }

        var parameters = ResolveParameters(context, context.GetRaw("parameters"));
        var result = await executor.ExecuteAsync(dataSourceId, endpoint, parameters, cancellationToken);
        if (!result.Succeeded)
        {
            return FlowActionResult.Fail(result.Error ?? "The data source call failed.", retryable: result.ErrorKind == Common.ResultErrorKind.Failure);
        }

        var response = result.Value!;
        var output = new JsonObject
        {
            ["statusCode"] = response.StatusCode,
            ["contentType"] = response.ContentType,
            ["durationMs"] = response.DurationMs,
            ["truncated"] = response.Truncated,
            ["body"] = response.Body.Length > MaxBodyLength ? response.Body[..MaxBodyLength] : response.Body,
        };
        if (!response.Truncated && LooksLikeJson(response.Body))
        {
            try
            {
                output["json"] = JsonNode.Parse(response.Body, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
            }
            catch (JsonException)
            {
                // Not JSON after all – only the text body is available.
            }
        }

        var success = response.StatusCode is >= 200 and < 300;
        if (!success && !string.Equals(context.GetRaw("failOnError"), "no", StringComparison.OrdinalIgnoreCase))
        {
            var retryable = response.StatusCode is 408 or 429 or >= 500;
            return FlowActionResult.Fail($"The endpoint returned HTTP {response.StatusCode}.", retryable, output);
        }

        return FlowActionResult.Success(output);
    }

    /// <summary>Resolves a key/value configuration (JSON object of templates) into text values.</summary>
    /// <param name="context">The context.</param>
    /// <param name="json">The JSON object.</param>
    /// <returns>The resolved values.</returns>
    internal static Dictionary<string, string> ResolveParameters(FlowActionContext context, string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            if (JsonNode.Parse(json) is JsonObject obj)
            {
                foreach (var pair in obj.Take(100))
                {
                    result[pair.Key] = context.Render(Templates.FlowTemplate.ToText(pair.Value));
                }
            }
        }
        catch (JsonException)
        {
            // Invalid configuration yields no parameters; the executor reports missing required ones.
        }

        return result;
    }

    private static bool LooksLikeJson(string body)
    {
        var trimmed = body.AsSpan().TrimStart();
        return trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[');
    }
}
