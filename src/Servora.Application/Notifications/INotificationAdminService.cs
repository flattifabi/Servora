using Servora.Application.Common;

namespace Servora.Application.Notifications;

/// <summary>Manages notifications and e-mail templates (permission <c>platform.notifications.manage</c>).</summary>
public interface INotificationAdminService
{
    /// <summary>Lists notifications.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The notifications (at most 1000).</returns>
    Task<Result<IReadOnlyList<NotificationModel>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a notification.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The notification.</returns>
    Task<Result<NotificationModel>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a notification.</summary>
    /// <param name="model">The notification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveAsync(NotificationModel model, CancellationToken cancellationToken = default);

    /// <summary>Deletes a notification.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists e-mail templates.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The templates (at most 500).</returns>
    Task<Result<IReadOnlyList<NotificationTemplateModel>>> ListTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or updates an e-mail template.</summary>
    /// <param name="model">The template.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveTemplateAsync(NotificationTemplateModel model, CancellationToken cancellationToken = default);

    /// <summary>Deletes an e-mail template that is not used by any notification.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Renders a notification for a record of its table (the given record or the most recent one the user can read).</summary>
    /// <param name="model">The (unsaved) notification.</param>
    /// <param name="recordId">The record, or null for the most recent record.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered message.</returns>
    Task<Result<RenderedNotification>> PreviewAsync(NotificationModel model, Guid? recordId, CancellationToken cancellationToken = default);
}
