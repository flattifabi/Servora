namespace Servora.Application.Notifications;

/// <summary>An outgoing e-mail.</summary>
/// <param name="To">The recipient addresses.</param>
/// <param name="Subject">The subject.</param>
/// <param name="HtmlBody">The HTML body (already encoded / sanitized).</param>
public sealed record EmailMessage(IReadOnlyList<string> To, string Subject, string HtmlBody)
{
    /// <summary>Gets the carbon copy recipients.</summary>
    public IReadOnlyList<string> Cc { get; init; } = [];

    /// <summary>Gets an optional plain text alternative body.</summary>
    public string? TextBody { get; init; }
}
