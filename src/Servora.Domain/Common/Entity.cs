namespace Servora.Domain.Common;

/// <summary>
/// Base type for every persisted entity. Uses a client-generated <see cref="Guid"/> (version 7, time ordered)
/// so identifiers are known before saving and index-friendly on all supported database providers.
/// </summary>
public abstract class Entity
{
    /// <summary>Gets or sets the unique identifier of the entity.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
}
