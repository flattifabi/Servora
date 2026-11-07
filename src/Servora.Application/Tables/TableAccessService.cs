using Servora.Application.Common;

namespace Servora.Application.Tables;

/// <summary>Default <see cref="ITableAccessService"/> based on <see cref="TablePolicies"/> and the actor's permissions.</summary>
/// <param name="currentUser">The current actor.</param>
public sealed class TableAccessService(ICurrentUser currentUser) : ITableAccessService
{
    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(string tableName, TableOperation operation, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        var policy = TablePolicies.Find(tableName);
        if (policy is not null)
        {
            var permission = policy.PermissionFor(operation);
            if (permission is null)
            {
                return false;
            }

            if (await currentUser.HasPermissionAsync(permission, cancellationToken))
            {
                return true;
            }
        }

        return await currentUser.HasPermissionAsync(TablePolicies.GenericPermission(tableName, operation), cancellationToken);
    }

    /// <inheritdoc />
    public bool IsFieldWritable(TableDefinition table, FieldDefinition field)
    {
        if (field.IsReadOnly)
        {
            return false;
        }

        if (field.IsCustom)
        {
            return true;
        }

        return TablePolicies.Find(table.Name)?.WritableFields is not { } writable || writable.Contains(field.Name);
    }
}
