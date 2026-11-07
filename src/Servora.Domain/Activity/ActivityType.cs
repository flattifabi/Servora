namespace Servora.Domain.Activity;

/// <summary>Kind of entry in a record's activity stream.</summary>
public enum ActivityType
{
    /// <summary>Customer-visible comment.</summary>
    Comment = 0,

    /// <summary>Internal work note, visible to fulfillers only.</summary>
    WorkNote = 1,

    /// <summary>Automatically recorded field change.</summary>
    FieldChange = 2,

    /// <summary>System event (state transition, SLA breach, approval, notification sent, ...).</summary>
    System = 3,
}
