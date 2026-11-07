namespace Servora.Application.Identity.Profile;

/// <summary>Editable fields of the own profile.</summary>
public sealed class MyProfileUpdate
{
    /// <summary>Gets or sets the display name (ignored for directory accounts).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the first name (ignored for directory accounts).</summary>
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the last name (ignored for directory accounts).</summary>
    public string? LastName { get; set; }

    /// <summary>Gets or sets the phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Gets or sets the preferred time zone identifier.</summary>
    public string? TimeZone { get; set; }

    /// <summary>Gets or sets the preferred theme (<c>dark</c>, <c>light</c>, <c>system</c>).</summary>
    public string? Theme { get; set; }
}
