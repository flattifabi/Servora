using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>Additional input of a state transition.</summary>
/// <param name="Note">An optional work note recorded with the transition.</param>
/// <param name="CloseCode">The close code (required when closing).</param>
/// <param name="CloseNotes">The close notes (required when closing).</param>
public sealed record ChangeTransitionInput(string? Note = null, ChangeCloseCode? CloseCode = null, string? CloseNotes = null);
