namespace Servora.Application.Identity.Admin;

/// <summary>Status filter of the user list.</summary>
public enum UserStatusFilter
{
    /// <summary>All users.</summary>
    All = 0,

    /// <summary>Active users only.</summary>
    Active = 1,

    /// <summary>Deactivated users only.</summary>
    Inactive = 2,

    /// <summary>Users that are currently locked out.</summary>
    Locked = 3,
}
