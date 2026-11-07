namespace Servora.Application.Common;

/// <summary>Type of the principal performing an operation.</summary>
public enum ActorType
{
    /// <summary>No authenticated principal.</summary>
    Anonymous = 0,

    /// <summary>Interactive user.</summary>
    User = 1,

    /// <summary>Caller authenticated with an API key (REST or MCP).</summary>
    ApiKey = 2,

    /// <summary>AI agent acting with its own scoped identity.</summary>
    Agent = 3,

    /// <summary>Background system process (scheduler, flow engine, sync job).</summary>
    System = 4,
}
