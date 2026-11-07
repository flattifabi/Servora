namespace Servora.Domain.Configuration;

/// <summary>Delivery state of an <see cref="OutboundEmail"/> in the mail outbox.</summary>
public enum OutboundEmailStatus
{
    /// <summary>Waiting for (the next) delivery attempt.</summary>
    Pending = 0,

    /// <summary>Claimed by the outbox processor and currently being handed to the SMTP server.</summary>
    Sending = 1,

    /// <summary>Accepted by the SMTP server.</summary>
    Sent = 2,

    /// <summary>Delivery failed permanently (all attempts used or the message was rejected as invalid).</summary>
    Failed = 3,
}
