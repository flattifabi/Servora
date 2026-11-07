namespace Servora.Domain.Common;

/// <summary>
/// Marks a property that must never be exposed through generic record access (Table API, generic forms, reports,
/// flows, MCP tools, exports), for example password hashes and security stamps.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class NotExposedAttribute : Attribute;
