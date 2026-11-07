namespace Servora.Application.Events;

/// <summary>Thrown by <c>SaveChangesAsync</c> when an <see cref="IRecordSavingHook"/> aborted the save.</summary>
/// <param name="message">The user-safe abort message.</param>
public sealed class RecordSaveAbortedException(string message) : Exception(message);
