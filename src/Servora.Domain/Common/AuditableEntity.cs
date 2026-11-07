namespace Servora.Domain.Common;

/// <summary>
/// Entity that tracks who created and last modified it. The values are maintained automatically by the
/// persistence layer on every save; application code should not set them manually.
/// </summary>
public abstract class AuditableEntity : Entity
{
    /// <summary>Gets or sets the UTC timestamp when the entity was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the display name of the actor (user, API key or system) that created the entity.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last modification.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the display name of the actor that last modified the entity.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the optimistic concurrency token. Incremented by the persistence layer on every update.
    /// </summary>
    public long Version { get; set; }
}
