namespace Servora.Application.Portal;

/// <summary>A consumer-friendly status of a portal item.</summary>
/// <param name="Label">The label shown to the requester, for example <c>Waiting for approval</c>.</param>
/// <param name="Tone">The pill tone: <c>info</c>, <c>primary</c>, <c>warning</c>, <c>success</c>, <c>danger</c> or <c>neutral</c>.</param>
/// <param name="IsOpen">Whether the item is still open (work or a decision is outstanding).</param>
/// <param name="NeedsAction">Whether the requester is expected to act (for example confirm a resolution).</param>
public sealed record PortalStatus(string Label, string Tone, bool IsOpen, bool NeedsAction = false);
