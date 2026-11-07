using System.Net;

namespace Servora.Application.Notifications;

/// <summary>Wraps rendered notification bodies in a simple, inline-styled HTML e-mail layout.</summary>
public static class EmailLayout
{
    /// <summary>Wraps a body fragment.</summary>
    /// <param name="title">The title (plain text, encoded here).</param>
    /// <param name="bodyHtml">The already encoded body fragment.</param>
    /// <param name="link">An optional link to the record (http/https or relative).</param>
    /// <returns>The complete HTML document.</returns>
    public static string Wrap(string title, string bodyHtml, string? link)
    {
        var button = string.IsNullOrWhiteSpace(link) || !(link.StartsWith('/') || link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || link.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ? string.Empty
            : $"<p style=\"margin:28px 0 0\"><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"display:inline-block;padding:10px 18px;border-radius:8px;background:#6366f1;color:#ffffff;text-decoration:none;font-weight:600\">Open record</a></p>";
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"></head>"
            + "<body style=\"margin:0;padding:24px;background:#f4f5f7;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#1f2330\">"
            + "<div style=\"max-width:640px;margin:0 auto;background:#ffffff;border-radius:12px;border:1px solid #e3e5ea;overflow:hidden\">"
            + "<div style=\"padding:18px 24px;background:linear-gradient(135deg,#6366f1,#8b5cf6);color:#ffffff;font-weight:700;font-size:15px\">Servora</div>"
            + $"<div style=\"padding:24px\"><h1 style=\"margin:0 0 16px;font-size:18px\">{WebUtility.HtmlEncode(title)}</h1>"
            + $"<div style=\"font-size:14px;line-height:1.6\">{bodyHtml}</div>{button}</div>"
            + "<div style=\"padding:14px 24px;border-top:1px solid #e3e5ea;color:#8a90a0;font-size:12px\">This is an automated message from Servora.</div>"
            + "</div></body></html>";
    }
}
