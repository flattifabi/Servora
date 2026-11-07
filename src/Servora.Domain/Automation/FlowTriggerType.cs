namespace Servora.Domain.Automation;

/// <summary>What starts a flow.</summary>
public enum FlowTriggerType
{
    /// <summary>Started manually (UI, business rule, REST API, MCP) or as a sub-flow.</summary>
    Manual = 0,

    /// <summary>A record of the trigger table was created.</summary>
    RecordCreated = 1,

    /// <summary>A record of the trigger table was updated.</summary>
    RecordUpdated = 2,

    /// <summary>A record of the trigger table was created or updated.</summary>
    RecordCreatedOrUpdated = 3,

    /// <summary>Time based (5-field cron expression).</summary>
    Schedule = 4,

    /// <summary>Authenticated inbound HTTP webhook (secret per flow).</summary>
    Webhook = 5,

    /// <summary>Polls an approved data source endpoint on a cron schedule.</summary>
    DataSourcePoll = 6,
}
