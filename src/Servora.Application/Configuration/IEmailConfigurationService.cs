using Servora.Application.Common;
using Servora.Domain.Configuration;

namespace Servora.Application.Configuration;

/// <summary>
/// Administration of outgoing e-mail: SMTP settings (password stored write-only in <c>ISecretStore</c>), test e-mails and
/// the mail outbox. Every method requires <c>admin.settings.manage</c>.
/// </summary>
public interface IEmailConfigurationService
{
    /// <summary>Gets the SMTP settings (without password).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings.</returns>
    Task<Result<SmtpSettings>> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and saves the SMTP settings; audited.</summary>
    /// <param name="update">The new settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SaveSettingsAsync(SmtpSettingsUpdate update, CancellationToken cancellationToken = default);

    /// <summary>Sends a test e-mail immediately with the saved settings (bypassing the queue) and records it in the outbox.</summary>
    /// <param name="recipient">The recipient address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result with the SMTP error on failure.</returns>
    Task<Result> SendTestAsync(string recipient, CancellationToken cancellationToken = default);

    /// <summary>Gets the outbox key figures.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<OutboxStatistics>> GetOutboxStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists outbox messages, newest first.</summary>
    /// <param name="request">Paging and search (recipient / subject).</param>
    /// <param name="status">An optional status filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<OutboxMessageSummary>>> ListOutboxAsync(PageRequest request, OutboundEmailStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>Gets the full content of an outbox message.</summary>
    /// <param name="id">The message identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The message.</returns>
    Task<Result<OutboxMessageDetail>> GetOutboxMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Re-queues a failed (or pending) message for immediate delivery with a fresh attempt budget.</summary>
    /// <param name="id">The message identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RetryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Re-queues all permanently failed messages (bounded).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of re-queued messages.</returns>
    Task<Result<int>> RetryAllFailedAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes a message that is not currently being sent.</summary>
    /// <param name="id">The message identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
