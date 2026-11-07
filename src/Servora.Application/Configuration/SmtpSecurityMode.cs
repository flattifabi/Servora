namespace Servora.Application.Configuration;

/// <summary>Transport security of the SMTP connection.</summary>
public enum SmtpSecurityMode
{
    /// <summary>Plain text connection. Only acceptable for relays on a trusted local network.</summary>
    None = 0,

    /// <summary>Upgrade the connection with STARTTLS (typically port 587 or 25). The server must support it.</summary>
    StartTls = 1,
}
