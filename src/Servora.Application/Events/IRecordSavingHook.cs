namespace Servora.Application.Events;

/// <summary>
/// Synchronous "before" hook invoked inside <c>SaveChangesAsync</c> for every pending insert / update / delete of a
/// registered table, before the database is written. Hooks may modify the entity (for example set calculated fields)
/// or abort the save via <see cref="RecordSavingContext.Abort"/>. Register as scoped services.
/// </summary>
public interface IRecordSavingHook
{
    /// <summary>Gets the execution order. Lower values run first.</summary>
    int Order => 100;

    /// <summary>Invoked for one pending change.</summary>
    /// <param name="context">The saving context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the hook is done.</returns>
    Task OnSavingAsync(RecordSavingContext context, CancellationToken cancellationToken);
}
