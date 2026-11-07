namespace Servora.Application.BusinessRules;

/// <summary>Declarative action of a business rule.</summary>
public enum BusinessRuleActionType
{
    /// <summary>Set a field to a fixed value (before rules change the pending record; after rules update it).</summary>
    SetField = 0,

    /// <summary>Abort the save with a message (before rules only).</summary>
    Abort = 1,

    /// <summary>Add a work note to the record (text with <c>${Field}</c> placeholders).</summary>
    AddWorkNote = 2,

    /// <summary>Start a flow with the record as input (after rules only).</summary>
    StartFlow = 3,

    /// <summary>Send a notification for the record (after rules only).</summary>
    SendNotification = 4,
}
