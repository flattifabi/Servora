namespace Servora.Application.Automation.Model;

/// <summary>
/// One step of a flow definition (serialized into <c>Flow.StepsJson</c>). Configuration values are strings that may
/// contain data pills (<c>{{trigger.record.ShortDescription}}</c>); structured settings (field maps, conditions) are
/// stored as JSON strings inside <see cref="Config"/>.
/// </summary>
public sealed class FlowStepDefinition
{
    /// <summary>Gets or sets the stable unique identifier inside the flow.</summary>
    public string Id { get; set; } = NewId();

    /// <summary>Gets or sets the data pill key (<c>steps.&lt;key&gt;</c>), unique inside the flow.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the action type key, for example <c>record.update</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the configuration values keyed by the action field key.</summary>
    public Dictionary<string, string?> Config { get; set; } = [];

    /// <summary>Gets or sets the branches (for example "then" / "else" of a condition).</summary>
    public List<FlowBranchDefinition> Branches { get; set; } = [];

    /// <summary>Gets or sets the number of retries for retryable failures (0–5).</summary>
    public int RetryCount { get; set; }

    /// <summary>Gets or sets the base delay between retries in seconds (doubles per attempt).</summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>Gets or sets a value indicating whether the flow continues when this step fails.</summary>
    public bool ContinueOnError { get; set; }

    /// <summary>Gets or sets a value indicating whether the step is skipped.</summary>
    public bool Disabled { get; set; }

    /// <summary>Gets or sets designer notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Creates a new short step identifier.</summary>
    /// <returns>A 12 character identifier.</returns>
    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>Gets a configuration value (case-insensitive key).</summary>
    /// <param name="key">The field key.</param>
    /// <returns>The raw value or <see langword="null"/>.</returns>
    public string? GetConfig(string key)
    {
        if (Config.TryGetValue(key, out var value))
        {
            return value;
        }

        foreach (var pair in Config)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>Finds a branch by key.</summary>
    /// <param name="key">The branch key.</param>
    /// <returns>The branch or <see langword="null"/>.</returns>
    public FlowBranchDefinition? FindBranch(string? key) =>
        key is null ? null : Branches.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase));
}
