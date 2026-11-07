namespace Servora.Domain.BusinessRules;

/// <summary>When a business rule runs relative to the database write.</summary>
public enum BusinessRuleTiming
{
    /// <summary>Synchronously before the record is written; may change field values or abort the save.</summary>
    Before = 0,

    /// <summary>Asynchronously after the change was committed (work notes, flows, notifications, follow-up updates).</summary>
    After = 1,
}
