using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>
/// Editable fields of a problem (create and update). State changes and resolution go through the dedicated actions of
/// <see cref="IProblemService"/>.
/// </summary>
public sealed class ProblemInput
{
    /// <summary>The maximum length of the analysis text fields.</summary>
    public const int AnalysisMaxLength = 8000;

    /// <summary>Gets or sets the one-line summary (required).</summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>Gets or sets the detailed description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the category name.</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the impact.</summary>
    public Impact Impact { get; set; } = Impact.Medium;

    /// <summary>Gets or sets the urgency.</summary>
    public Urgency Urgency { get; set; } = Urgency.Medium;

    /// <summary>Gets or sets the assignment group.</summary>
    public Guid? AssignmentGroupId { get; set; }

    /// <summary>Gets or sets the assignee (problem coordinator).</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the affected configuration item.</summary>
    public Guid? ConfigurationItemId { get; set; }

    /// <summary>Gets or sets the affected business service.</summary>
    public Guid? BusinessServiceId { get; set; }

    /// <summary>Gets or sets a value indicating whether the problem is a known error.</summary>
    public bool IsKnownError { get; set; }

    /// <summary>Gets or sets the workaround.</summary>
    public string? Workaround { get; set; }

    /// <summary>Gets or sets the root cause.</summary>
    public string? RootCause { get; set; }

    /// <summary>Gets or sets the fix notes.</summary>
    public string? FixNotes { get; set; }

    /// <summary>Gets or sets comma separated tags.</summary>
    public string? Tags { get; set; }

    /// <summary>Validates the input.</summary>
    /// <returns>A user-safe error message, or <see langword="null"/> when valid.</returns>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(ShortDescription))
        {
            return "A short description is required.";
        }

        if (ShortDescription.Trim().Length > IncidentInput.ShortDescriptionMaxLength)
        {
            return $"The short description must not exceed {IncidentInput.ShortDescriptionMaxLength} characters.";
        }

        if (Description?.Length > IncidentInput.DescriptionMaxLength)
        {
            return $"The description must not exceed {IncidentInput.DescriptionMaxLength:N0} characters.";
        }

        if (Workaround?.Length > AnalysisMaxLength || RootCause?.Length > AnalysisMaxLength || FixNotes?.Length > AnalysisMaxLength)
        {
            return $"Workaround, root cause and fix notes must not exceed {AnalysisMaxLength:N0} characters.";
        }

        if (Category?.Length > 100)
        {
            return "The category must not exceed 100 characters.";
        }

        if (Tags?.Length > 1000)
        {
            return "Tags must not exceed 1,000 characters.";
        }

        if (!Enum.IsDefined(Impact) || !Enum.IsDefined(Urgency))
        {
            return "Impact or urgency has an invalid value.";
        }

        return null;
    }
}
