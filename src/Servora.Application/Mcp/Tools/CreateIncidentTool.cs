using System.Globalization;
using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Common;

namespace Servora.Application.Mcp.Tools;

/// <summary>Creates an incident through <see cref="IRecordService"/>. Consequential: AI agents need user confirmation.</summary>
/// <param name="records">The record service.</param>
/// <param name="currentUser">The current actor.</param>
public sealed class CreateIncidentTool(IRecordService records, ICurrentUser currentUser) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "create_incident";

    /// <inheritdoc />
    public override string Title => "Create incident";

    /// <inheritdoc />
    public override string Description =>
        "Creates a new incident. Impact and urgency use 1 = high, 2 = medium, 3 = low; the priority is calculated automatically.";

    /// <inheritdoc />
    public override string? RequiredPermission => Permissions.Incidents.Create;

    /// <inheritdoc />
    public override bool IsReadOnly => false;

    /// <inheritdoc />
    public override bool IsConsequential => true;

    /// <inheritdoc />
    protected override string SchemaJson => """
        {
          "type": "object",
          "properties": {
            "short_description": { "type": "string", "description": "One-line summary.", "minLength": 3, "maxLength": 160 },
            "description": { "type": "string", "description": "Detailed description.", "maxLength": 4000 },
            "impact": { "type": "integer", "description": "1 = high, 2 = medium, 3 = low (default 3).", "minimum": 1, "maximum": 3 },
            "urgency": { "type": "integer", "description": "1 = high, 2 = medium, 3 = low (default 3).", "minimum": 1, "maximum": 3 }
          },
          "required": ["short_description"],
          "additionalProperties": false
        }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ShortDescription"] = McpJson.GetString(arguments, "short_description"),
            ["Impact"] = McpJson.GetInt(arguments, "impact", 3).ToString(CultureInfo.InvariantCulture),
            ["Urgency"] = McpJson.GetInt(arguments, "urgency", 3).ToString(CultureInfo.InvariantCulture),
        };

        if (McpJson.GetString(arguments, "description") is { } description)
        {
            values["Description"] = description;
        }

        if (currentUser.UserId is { } userId)
        {
            values["OpenedById"] = userId.ToString();
        }

        var created = await records.CreateAsync(TableNames.Incident, values, cancellationToken);
        if (!created.Succeeded)
        {
            return Fail(created);
        }

        var record = await records.GetAsync(TableNames.Incident, created.Value, cancellationToken);
        var number = record.Succeeded && record.Value!.TryGetValue("Number", out var n) ? n?.ToString() : null;
        return Ok(new { id = created.Value, number, table = TableNames.Incident, message = $"Incident {number ?? created.Value.ToString()} created." });
    }
}
