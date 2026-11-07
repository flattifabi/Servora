namespace Servora.Application.Configuration;

/// <summary>Metadata of a stored secret. The value is never part of any read model.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Name">The unique name.</param>
/// <param name="Description">The description.</param>
/// <param name="CreatedAt">When the secret was created.</param>
/// <param name="CreatedBy">Who created the secret.</param>
/// <param name="RotatedAt">When the value was last replaced.</param>
/// <param name="UpdatedAt">When the secret was last modified.</param>
/// <param name="UpdatedBy">Who last modified the secret.</param>
public sealed record SecretSummary(Guid Id, string Name, string? Description, DateTimeOffset CreatedAt, string? CreatedBy, DateTimeOffset? RotatedAt, DateTimeOffset UpdatedAt, string? UpdatedBy)
{
    /// <summary>Gets the time the current value was set (rotation time or creation time).</summary>
    public DateTimeOffset ValueSetAt => RotatedAt ?? CreatedAt;

    /// <summary>Gets a value indicating whether the secret is used by a built-in platform feature.</summary>
    public bool IsSystem => SecretNameRules.IsSystemSecret(Name);
}
