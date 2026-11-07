using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Model;
using Servora.Domain.Automation;
using Servora.Domain.Integration;

namespace Servora.Web.Components.Automation;

/// <summary>Presentation helpers (labels, icons, tones, formatting) shared by the automation and integration pages.</summary>
public static class FlowUi
{
    // Display only: Blazor HTML-encodes the rendered text, so relaxed escaping keeps "+", quotes and umlauts readable.
    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Gets the label of a trigger type.</summary>
    /// <param name="type">The trigger type.</param>
    /// <returns>The label.</returns>
    public static string TriggerLabel(FlowTriggerType type) => type switch
    {
        FlowTriggerType.RecordCreated => "Record created",
        FlowTriggerType.RecordUpdated => "Record updated",
        FlowTriggerType.RecordCreatedOrUpdated => "Record created or updated",
        FlowTriggerType.Schedule => "Schedule",
        FlowTriggerType.Webhook => "Inbound webhook",
        FlowTriggerType.DataSourcePoll => "Data source poll",
        _ => "Manual / sub-flow",
    };

    /// <summary>Gets the icon of a trigger type.</summary>
    /// <param name="type">The trigger type.</param>
    /// <returns>The icon name.</returns>
    public static string TriggerIcon(FlowTriggerType type) => type switch
    {
        FlowTriggerType.RecordCreated => "plus",
        FlowTriggerType.RecordUpdated => "edit",
        FlowTriggerType.RecordCreatedOrUpdated => "database",
        FlowTriggerType.Schedule => "calendar-clock",
        FlowTriggerType.Webhook => "webhook",
        FlowTriggerType.DataSourcePoll => "plug",
        _ => "play",
    };

    /// <summary>Gets the pill tone of a run status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The tone.</returns>
    public static string RunTone(FlowRunStatus status) => status switch
    {
        FlowRunStatus.Succeeded => "success",
        FlowRunStatus.Failed => "danger",
        FlowRunStatus.Waiting => "warning",
        FlowRunStatus.Running => "primary",
        FlowRunStatus.Cancelled => "neutral",
        _ => "info",
    };

    /// <summary>Gets the pill tone of a step status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The tone.</returns>
    public static string StepTone(FlowStepStatus status) => status switch
    {
        FlowStepStatus.Succeeded => "success",
        FlowStepStatus.Failed => "danger",
        FlowStepStatus.Waiting => "warning",
        FlowStepStatus.Running => "primary",
        _ => "neutral",
    };

    /// <summary>Gets the icon of a step status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The icon name.</returns>
    public static string StepIcon(FlowStepStatus status) => status switch
    {
        FlowStepStatus.Succeeded => "check",
        FlowStepStatus.Failed => "x",
        FlowStepStatus.Waiting => "pause",
        FlowStepStatus.Running => "loader",
        _ => "minus",
    };

    /// <summary>Gets the pill tone of an import run status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The tone.</returns>
    public static string ImportTone(ImportRunStatus status) => status switch
    {
        ImportRunStatus.Completed => "success",
        ImportRunStatus.CompletedWithErrors => "warning",
        ImportRunStatus.Failed => "danger",
        _ => "primary",
    };

    /// <summary>Gets the label of an import run status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The label.</returns>
    public static string ImportLabel(ImportRunStatus status) => status == ImportRunStatus.CompletedWithErrors ? "Completed with errors" : status.ToString();

    /// <summary>Gets the label of an authentication type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The label.</returns>
    public static string AuthLabel(DataSourceAuthType type) => type switch
    {
        DataSourceAuthType.ApiKeyHeader => "API key header",
        DataSourceAuthType.Bearer => "Bearer token",
        DataSourceAuthType.Basic => "Basic (TLS)",
        DataSourceAuthType.OAuth2ClientCredentials => "OAuth 2.0 client credentials",
        _ => "None",
    };

    /// <summary>Formats a duration in milliseconds.</summary>
    /// <param name="milliseconds">The duration.</param>
    /// <returns>The text.</returns>
    public static string Duration(long? milliseconds) => milliseconds switch
    {
        null => "—",
        < 1000 => $"{milliseconds} ms",
        < 60_000 => (milliseconds.Value / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s",
        < 3_600_000 => $"{milliseconds.Value / 60_000} min {milliseconds.Value % 60_000 / 1000} s",
        _ => $"{milliseconds.Value / 3_600_000} h {milliseconds.Value % 3_600_000 / 60_000} min",
    };

    /// <summary>Formats a byte count.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text.</returns>
    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => (bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB",
        _ => (bytes / 1024.0 / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " MB",
    };

    /// <summary>Pretty-prints JSON; non-JSON text is returned unchanged.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The formatted text.</returns>
    public static string PrettyJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(document.RootElement, s_indented);
        }
        catch (JsonException)
        {
            return text;
        }
    }

    /// <summary>Gets a short version of an identifier.</summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The first eight hex characters.</returns>
    public static string Short(Guid id) => id.ToString("N")[..8];

    /// <summary>Gets the CSS color variable of a design tone.</summary>
    /// <param name="tone">The tone (primary, accent, success, warning, danger, info, purple, neutral).</param>
    /// <returns>The CSS color expression.</returns>
    public static string ToneColor(string? tone) => tone switch
    {
        "accent" => "var(--accent)",
        "success" => "var(--success)",
        "warning" => "var(--warning)",
        "danger" => "var(--danger)",
        "info" => "var(--info)",
        "purple" => "var(--purple)",
        "neutral" => "var(--text-muted)",
        _ => "var(--primary)",
    };

    /// <summary>Gets the soft background CSS variable of a design tone.</summary>
    /// <param name="tone">The tone.</param>
    /// <returns>The CSS color expression.</returns>
    public static string ToneSoft(string? tone) => tone switch
    {
        "accent" => "var(--accent-soft)",
        "success" => "var(--success-soft)",
        "warning" => "var(--warning-soft)",
        "danger" => "var(--danger-soft)",
        "info" => "var(--info-soft)",
        "purple" => "var(--purple-soft)",
        "neutral" => "var(--neutral-soft)",
        _ => "var(--primary-soft)",
    };

    /// <summary>Builds a one-line, human readable summary of a step's configuration for the canvas card.</summary>
    /// <param name="step">The step.</param>
    /// <param name="descriptor">The step type descriptor (may be <see langword="null"/> for unknown types).</param>
    /// <returns>The summary or <see langword="null"/>.</returns>
    public static string? StepSummary(FlowStepDefinition step, FlowActionDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return $"Unknown step type '{step.Type}'";
        }

        var parts = new List<string>();
        foreach (var field in descriptor.Fields)
        {
            var value = step.GetConfig(field.Key);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (field.Kind)
            {
                case FlowActionFieldKind.Table:
                case FlowActionFieldKind.DataSourceEndpoint:
                case FlowActionFieldKind.Text:
                case FlowActionFieldKind.Multiline:
                case FlowActionFieldKind.Number:
                    parts.Add(value.Length > 48 ? value[..48] + "…" : value);
                    break;
                case FlowActionFieldKind.Select:
                    var option = field.Options.FirstOrDefault(o => o.Value == value);
                    if (option is not null && value != field.DefaultValue)
                    {
                        parts.Add(option.Label);
                    }

                    break;
                case FlowActionFieldKind.Condition:
                    var condition = FlowJson.TryDeserialize<FlowCondition>(value);
                    var count = condition?.Rules.Count ?? 0;
                    parts.Add(count == 0 ? "always true" : $"{count} rule{(count == 1 ? string.Empty : "s")} · {(condition!.MatchAny ? "any" : "all")}");
                    break;
                case FlowActionFieldKind.RecordValues:
                    var values = FlowJson.TryDeserialize<List<FlowFieldValue>>(value);
                    if (values is { Count: > 0 })
                    {
                        parts.Add(string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v.Field)).Take(3).Select(v => v.Field)) + (values.Count > 3 ? $" +{values.Count - 3}" : string.Empty));
                    }

                    break;
            }

            if (parts.Count >= 2)
            {
                break;
            }
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
