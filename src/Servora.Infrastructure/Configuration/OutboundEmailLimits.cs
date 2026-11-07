namespace Servora.Infrastructure.Configuration;

/// <summary>Column limits of the mail outbox table (<c>sys_email_outbox</c>).</summary>
internal static class OutboundEmailLimits
{
    /// <summary>The maximum length of the recipient lists.</summary>
    public const int MaxRecipientsLength = 4000;

    /// <summary>The maximum length of the subject.</summary>
    public const int MaxSubjectLength = 998;

    /// <summary>The maximum length of a body.</summary>
    public const int MaxBodyLength = 1_000_000;

    /// <summary>The maximum length of the last error.</summary>
    public const int MaxErrorLength = 2000;
}
