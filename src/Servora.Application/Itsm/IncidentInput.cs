using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>
/// Editable fields of an incident (create and update). State, resolution and assignment shortcuts are changed through
/// the dedicated actions of <see cref="IIncidentService"/> so that transitions are always validated.
/// </summary>
public sealed class IncidentInput
{
    /// <summary>The maximum length of the short description.</summary>
    public const int ShortDescriptionMaxLength = 300;

    /// <summary>The maximum length of the description.</summary>
    public const int DescriptionMaxLength = 32_000;

    /// <summary>Gets or sets the one-line summary (required).</summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>Gets or sets the detailed description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the caller.</summary>
    public Guid? CallerId { get; set; }

    /// <summary>Gets or sets the category name.</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the subcategory name.</summary>
    public string? Subcategory { get; set; }

    /// <summary>Gets or sets the contact channel.</summary>
    public ContactChannel Channel { get; set; } = ContactChannel.Phone;

    /// <summary>Gets or sets the impact.</summary>
    public Impact Impact { get; set; } = Impact.Low;

    /// <summary>Gets or sets the urgency.</summary>
    public Urgency Urgency { get; set; } = Urgency.Low;

    /// <summary>Gets or sets the assignment group.</summary>
    public Guid? AssignmentGroupId { get; set; }

    /// <summary>Gets or sets the assignee.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the affected configuration item.</summary>
    public Guid? ConfigurationItemId { get; set; }

    /// <summary>Gets or sets the affected business service.</summary>
    public Guid? BusinessServiceId { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a major incident.</summary>
    public bool IsMajorIncident { get; set; }

    /// <summary>Gets or sets the parent incident.</summary>
    public Guid? ParentIncidentId { get; set; }

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

        if (ShortDescription.Trim().Length > ShortDescriptionMaxLength)
        {
            return $"The short description must not exceed {ShortDescriptionMaxLength} characters.";
        }

        if (Description?.Length > DescriptionMaxLength)
        {
            return $"The description must not exceed {DescriptionMaxLength:N0} characters.";
        }

        if (Category?.Length > 100 || Subcategory?.Length > 100)
        {
            return "Category names must not exceed 100 characters.";
        }

        if (Tags?.Length > 1000)
        {
            return "Tags must not exceed 1,000 characters.";
        }

        if (!Enum.IsDefined(Channel) || !Enum.IsDefined(Impact) || !Enum.IsDefined(Urgency))
        {
            return "Channel, impact or urgency has an invalid value.";
        }

        return null;
    }
}
