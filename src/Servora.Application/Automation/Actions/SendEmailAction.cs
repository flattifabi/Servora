using System.Net;
using System.Net.Mail;
using System.Text.Json.Nodes;
using Servora.Application.Notifications;

namespace Servora.Application.Automation.Actions;

/// <summary>
/// Sends an e-mail through <see cref="IEmailSender"/>. The body is treated as plain text: literal text and data pill
/// values are HTML-encoded and line breaks are preserved, so record content can never inject markup.
/// </summary>
/// <param name="email">The e-mail sender.</param>
public sealed class SendEmailAction(IEmailSender email) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "email.send";

    /// <summary>The maximum number of recipients.</summary>
    public const int MaxRecipients = 50;

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Send e-mail", "Communication", "mail", "accent", "Send an e-mail with data pills in recipients, subject and body.")
    {
        Fields =
        [
            new FlowActionField("to", "To", FlowActionFieldKind.Text) { Required = true, Help = "Comma separated addresses or data pills." },
            new FlowActionField("cc", "Cc", FlowActionFieldKind.Text),
            new FlowActionField("subject", "Subject", FlowActionFieldKind.Text) { Required = true },
            new FlowActionField("body", "Message", FlowActionFieldKind.Multiline) { Required = true },
        ],
        Outputs = [new FlowActionOutput("sent", "Sent"), new FlowActionOutput("recipients", "Recipient count")],
    };

    /// <inheritdoc />
    public async Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var to = ParseAddresses(context.GetText("to"));
        var cc = ParseAddresses(context.GetText("cc"));
        if (to.Count == 0)
        {
            return FlowActionResult.Fail("No valid recipient address.");
        }

        if (to.Count + cc.Count > MaxRecipients)
        {
            return FlowActionResult.Fail($"Too many recipients (maximum {MaxRecipients}).");
        }

        var subject = context.GetText("subject").Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (subject.Length > 250)
        {
            subject = subject[..250];
        }

        var text = context.GetText("body");
        var html = "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.5\">"
            + WebUtility.HtmlEncode(text).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br />", StringComparison.Ordinal)
            + "</div>";
        var sent = await email.SendAsync(new EmailMessage(to, subject, html) { Cc = cc, TextBody = text }, cancellationToken);
        return FlowActionResult.Success(new JsonObject { ["sent"] = sent, ["recipients"] = to.Count + cc.Count, ["subject"] = subject });
    }

    /// <summary>Parses and validates a list of addresses.</summary>
    /// <param name="text">The comma / semicolon separated text.</param>
    /// <returns>The valid, distinct addresses.</returns>
    internal static IReadOnlyList<string> ParseAddresses(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var result = new List<string>();
        foreach (var part in text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (MailAddress.TryCreate(part, out var address) && !result.Contains(address.Address, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(address.Address);
            }
        }

        return result;
    }
}
