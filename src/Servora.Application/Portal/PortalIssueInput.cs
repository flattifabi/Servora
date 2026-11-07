using Servora.Domain.Itsm;

namespace Servora.Application.Portal;

/// <summary>What a portal user enters on the "Report an issue" form.</summary>
public sealed class PortalIssueInput
{
    /// <summary>The maximum length of the summary.</summary>
    public const int SummaryMaxLength = 160;

    /// <summary>The maximum length of the description.</summary>
    public const int DescriptionMaxLength = 8000;

    /// <summary>Gets or sets the one-line summary.</summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>Gets or sets the detailed description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the optional category name (must be an active top-level incident category).</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets how strongly the issue blocks the user's work.</summary>
    public Urgency Urgency { get; set; } = Urgency.Medium;

    /// <summary>Gets or sets how many people are affected.</summary>
    public Impact Impact { get; set; } = Impact.Low;

    /// <summary>Validates the input.</summary>
    /// <returns>A user-safe error message, or <see langword="null"/> when the input is valid.</returns>
    public string? Validate()
    {
        var summary = ShortDescription?.Trim() ?? string.Empty;
        if (summary.Length < 5)
        {
            return "Please describe the issue in a few words (at least 5 characters).";
        }

        if (summary.Length > SummaryMaxLength)
        {
            return $"The summary must not exceed {SummaryMaxLength} characters.";
        }

        if (Description is { Length: > DescriptionMaxLength })
        {
            return $"The description must not exceed {DescriptionMaxLength} characters.";
        }

        if (!Enum.IsDefined(Urgency) || !Enum.IsDefined(Impact))
        {
            return "Please select how the issue affects you.";
        }

        return null;
    }
}
