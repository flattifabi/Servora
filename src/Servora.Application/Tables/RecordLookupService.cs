using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Directory;
using Servora.Domain.Common;

namespace Servora.Application.Tables;

/// <summary>
/// Default <see cref="IRecordLookupService"/>: users, groups and configuration items use the directory look-ups (as all
/// kernel pickers do); every other table goes through the permission-checked <see cref="IRecordService"/>.
/// </summary>
/// <param name="records">The record service.</param>
/// <param name="registry">The table registry.</param>
/// <param name="directory">The directory look-up service.</param>
public sealed class RecordLookupService(IRecordService records, ITableRegistry registry, IDirectoryLookupService directory) : IRecordLookupService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupItem>> SearchAsync(string tableName, string? term, int take = 10, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 50);
        switch (tableName)
        {
            case TableNames.User:
                return await directory.SearchUsersAsync(term, take, cancellationToken);
            case TableNames.Group:
                return await directory.SearchGroupsAsync(term, take, cancellationToken);
            case TableNames.ConfigurationItem:
                return await directory.SearchConfigurationItemsAsync(term, take, cancellationToken);
        }

        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return [];
        }

        var result = await records.QueryAsync(table.Name, new RecordQuery
        {
            Page = new PageRequest { PageSize = take, Search = string.IsNullOrWhiteSpace(term) ? null : term.Trim(), SortBy = table.DisplayField },
            Fields = [table.DisplayField],
            IncludeDisplayValues = false,
        }, cancellationToken);
        return result.Succeeded ? result.Value!.Items.Select(r => ToItem(table, r)).ToList() : [];
    }

    /// <inheritdoc />
    public async Task<LookupItem?> ResolveAsync(string tableName, Guid id, CancellationToken cancellationToken = default)
    {
        switch (tableName)
        {
            case TableNames.User:
                return (await directory.GetUsersAsync([id], cancellationToken)).GetValueOrDefault(id);
            case TableNames.Group:
                return (await directory.GetGroupsAsync([id], cancellationToken)).GetValueOrDefault(id);
            case TableNames.ConfigurationItem:
                return (await directory.GetConfigurationItemsAsync([id], cancellationToken)).GetValueOrDefault(id);
        }

        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return null;
        }

        var result = await records.GetAsync(table.Name, id, cancellationToken);
        return result.Succeeded ? ToItem(table, result.Value!) : null;
    }

    private static LookupItem ToItem(TableDefinition table, IReadOnlyDictionary<string, object?> row)
    {
        var id = ConditionValueConverter.TryGetGuid(row.GetValueOrDefault(nameof(Entity.Id)), out var g) ? g : Guid.Empty;
        var display = ConditionValueConverter.ToText(row.GetValueOrDefault(table.DisplayField));
        return new LookupItem(id, string.IsNullOrWhiteSpace(display) ? id.ToString() : display, table.Label);
    }
}
