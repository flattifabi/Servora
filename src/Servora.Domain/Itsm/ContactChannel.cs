namespace Servora.Domain.Itsm;

/// <summary>Channel through which an incident was reported.</summary>
public enum ContactChannel
{
    /// <summary>Self-service portal.</summary>
    Portal = 1,

    /// <summary>Phone call to the service desk.</summary>
    Phone = 2,

    /// <summary>E-mail.</summary>
    Email = 3,

    /// <summary>Automatically raised by a monitoring system.</summary>
    Monitoring = 4,

    /// <summary>Chat or messaging.</summary>
    Chat = 5,
}
