namespace Servora.Application.Common;

/// <summary>
/// Scoped holder that lets background services, engines and AI agents run code under an explicit actor. When
/// <see cref="Override"/> is set, <see cref="ICurrentUser"/> reports that actor instead of the HTTP / circuit user.
/// </summary>
public sealed class ActorContext
{
    /// <summary>Gets the explicitly set actor, if any.</summary>
    public ActorInfo? Override { get; private set; }

    /// <summary>Gets the correlation identifier for background executions.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>
    /// Gets or sets the record event cascade depth of the current scope. Record events raised by saves in this scope
    /// carry this depth; the event processor stops cascades beyond a fixed limit to prevent infinite loops.
    /// </summary>
    public int EventDepth { get; set; }

    /// <summary>Sets the actor for the remainder of the scope.</summary>
    /// <param name="actor">The actor.</param>
    /// <param name="correlationId">An optional correlation id.</param>
    public void Set(ActorInfo actor, string? correlationId = null)
    {
        Override = actor;
        CorrelationId = correlationId ?? CorrelationId ?? Guid.NewGuid().ToString("N");
    }
}
