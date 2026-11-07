namespace Servora.Application.Tables;

/// <summary>
/// Generic-access policy of a typed table owned by a module: the module permission required per operation
/// (<see langword="null"/> = the operation is not available through generic record access) and optional field
/// restrictions for generic writes.
/// </summary>
/// <param name="Read">The permission required to read records.</param>
/// <param name="Create">The permission required to create records, or null when generic creation is not allowed.</param>
/// <param name="Write">The permission required to update records, or null when generic updates are not allowed.</param>
/// <param name="Delete">The permission required to delete records, or null when generic deletion is not allowed.</param>
public sealed record TablePolicy(string Read, string? Create, string? Write, string? Delete)
{
    /// <summary>Gets the fields that may be written generically (custom fields are always writable); null means all non read-only fields.</summary>
    public IReadOnlySet<string>? WritableFields { get; init; }

    /// <summary>Gets the number prefix used when a record is created without number, for example <c>INC</c>.</summary>
    public string? NumberPrefix { get; init; }

    /// <summary>Gets the permission required for an operation.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The permission key or <see langword="null"/>.</returns>
    public string? PermissionFor(TableOperation operation) => operation switch
    {
        TableOperation.Read => Read,
        TableOperation.Create => Create,
        TableOperation.Write => Write,
        _ => Delete,
    };
}
