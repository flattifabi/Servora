namespace Servora.Application.Automation.Services;

/// <summary>Parameters of a designer test run.</summary>
public sealed record FlowTestRequest
{
    /// <summary>Gets the input values.</summary>
    public IReadOnlyDictionary<string, string> Inputs { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the record used as trigger record for record-triggered flows.</summary>
    public Guid? RecordId { get; init; }

    /// <summary>Gets a sample JSON payload for webhook / poll triggers.</summary>
    public string? PayloadJson { get; init; }
}
