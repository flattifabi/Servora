using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>Input model for creating or editing a user.</summary>
public sealed class UserEditModel
{
    /// <summary>Gets or sets the login name (only used when creating).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name (derived from first / last name when empty).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the last name.</summary>
    public string? LastName { get; set; }

    /// <summary>Gets or sets the e-mail address.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Gets or sets the job title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the department.</summary>
    public string? Department { get; set; }

    /// <summary>Gets or sets the location.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets the manager.</summary>
    public Guid? ManagerId { get; set; }

    /// <summary>Gets or sets the preferred time zone identifier.</summary>
    public string? TimeZone { get; set; }

    /// <summary>Gets or sets the account source when creating (<see cref="UserSource.Local"/> or <see cref="UserSource.Service"/>).</summary>
    public UserSource Source { get; set; } = UserSource.Local;

    /// <summary>Gets or sets the initial password for new local accounts (validated against the password policy).</summary>
    public string? InitialPassword { get; set; }

    /// <summary>Gets or sets a value indicating whether a new local account must change the password at first sign-in.</summary>
    public bool MustChangePassword { get; set; } = true;
}
