using Servora.Domain.Common;

namespace Servora.Domain.Configuration;

/// <summary>
/// An encrypted credential that the platform must be able to use again (LDAP bind password, SMTP password,
/// data source token, AI provider key). The value is protected with ASP.NET Core Data Protection and is never
/// returned to the UI after it was saved.
/// </summary>
public class StoredSecret : AuditableEntity
{
    /// <summary>Gets or sets the unique secret name, for example <c>ldap.bind_password</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the protected (encrypted) value. Never exposed through generic record access.</summary>
    [NotExposed]
    public string ProtectedValue { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC time of the last rotation.</summary>
    public DateTimeOffset? RotatedAt { get; set; }
}
