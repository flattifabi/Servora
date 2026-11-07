namespace Servora.Application.Configuration;

/// <summary>A historic change of configuration, read from the audit log.</summary>
/// <param name="OccurredAt">When the change happened.</param>
/// <param name="Actor">Who made the change.</param>
/// <param name="Action">The audit action, for example <c>setting.updated</c>.</param>
/// <param name="Target">The changed key or object.</param>
/// <param name="Message">The audit message (secret-free).</param>
public sealed record SettingChange(DateTimeOffset OccurredAt, string Actor, string Action, string? Target, string? Message);
