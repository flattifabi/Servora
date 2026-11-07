using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Notifications;

namespace Servora.Tests.Unit.Configuration;

/// <summary><see cref="ISmtpTransport"/> that records messages and returns a configurable result.</summary>
internal sealed class RecordingSmtpTransport : ISmtpTransport
{
    /// <summary>Gets or sets the result returned for every send.</summary>
    public Result NextResult { get; set; } = Result.Success();

    /// <summary>Gets the sent messages with the options used.</summary>
    public List<(SmtpConnectionOptions Options, EmailMessage Message)> Sent { get; } = [];

    /// <inheritdoc />
    public Task<Result> SendAsync(SmtpConnectionOptions options, EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add((options, message));
        return Task.FromResult(NextResult);
    }
}
