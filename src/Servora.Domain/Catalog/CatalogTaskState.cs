namespace Servora.Domain.Catalog;

/// <summary>State of a catalog (fulfilment) task.</summary>
public enum CatalogTaskState
{
    /// <summary>Not started.</summary>
    Open = 0,

    /// <summary>Work in progress.</summary>
    WorkInProgress = 1,

    /// <summary>Completed (final).</summary>
    Completed = 2,

    /// <summary>Cancelled / closed incomplete (final).</summary>
    Cancelled = 3,
}
