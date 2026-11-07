using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>Create or update a saved report.</summary>
/// <param name="Id">The report to update, or null to create a new one.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="Spec">The report specification.</param>
/// <param name="Visibility">Who can see the report.</param>
/// <param name="SharedWithRole">The role name when <paramref name="Visibility"/> is <see cref="ReportVisibility.Role"/>.</param>
public sealed record ReportSaveRequest(Guid? Id, string Name, string? Description, ReportSpec Spec, ReportVisibility Visibility, string? SharedWithRole);
