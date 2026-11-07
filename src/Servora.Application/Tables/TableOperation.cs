namespace Servora.Application.Tables;

/// <summary>A generic operation on table records, checked by <see cref="ITableAccessService"/>.</summary>
public enum TableOperation
{
    /// <summary>Read records.</summary>
    Read = 0,

    /// <summary>Create records.</summary>
    Create = 1,

    /// <summary>Update records.</summary>
    Write = 2,

    /// <summary>Delete records.</summary>
    Delete = 3,
}
