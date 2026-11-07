using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Tables;
using Servora.Domain.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Platform;

/// <summary>
/// Default <see cref="IRecordNumberResolver"/>: maps the letter prefix to the owning table (built-in prefixes and
/// custom table prefixes) and looks the record up through the permission-checked <see cref="IRecordService"/>.
/// </summary>
/// <param name="records">The record service.</param>
/// <param name="registry">The table registry.</param>
/// <param name="dbFactory">The database context factory.</param>
public sealed partial class RecordNumberResolver(IRecordService records, ITableRegistry registry, IAppDbContextFactory dbFactory) : IRecordNumberResolver
{
    /// <inheritdoc />
    public async Task<Result<RecordLocation>> ResolveAsync(string number, CancellationToken cancellationToken = default)
    {
        var normalized = number?.Trim().ToUpperInvariant() ?? string.Empty;
        var match = NumberPattern().Match(normalized);
        if (!match.Success)
        {
            return Result<RecordLocation>.Invalid("This is not a valid record number.");
        }

        var prefix = match.Groups["prefix"].Value;
        var candidates = new List<string>();
        if (TablePolicies.NumberPrefixes.TryGetValue(prefix, out var typed))
        {
            candidates.Add(typed);
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            candidates.AddRange(await db.Set<CustomTable>().AsNoTracking().Where(t => t.NumberPrefix == prefix).Select(t => t.Name).Take(5).ToListAsync(cancellationToken));
        }

        var forbidden = false;
        foreach (var tableName in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var table = await registry.GetTableAsync(tableName, cancellationToken);
            if (table?.FindField(CustomTableDefinitionBuilder.NumberField) is null)
            {
                continue;
            }

            var result = await records.QueryAsync(table.Name, new RecordQuery
            {
                Condition = new ConditionGroup { Rules = [new ConditionRule { Field = CustomTableDefinitionBuilder.NumberField, Operator = ConditionOperator.Equals, Value = normalized }] },
                Page = new PageRequest { PageSize = 1 },
                Fields = [CustomTableDefinitionBuilder.NumberField],
                IncludeDisplayValues = false,
            }, cancellationToken);
            if (result.ErrorKind == ResultErrorKind.Forbidden)
            {
                forbidden = true;
                continue;
            }

            if (result.Succeeded && result.Value!.Items.Count > 0 && ConditionValueConverter.TryGetGuid(result.Value.Items[0].GetValueOrDefault(nameof(Entity.Id)), out var id))
            {
                return Result<RecordLocation>.Success(new RecordLocation(table.Name, id, RecordUrls.For(table, id)));
            }
        }

        return forbidden
            ? Result<RecordLocation>.Forbidden("You do not have permission to open this record.")
            : Result<RecordLocation>.NotFound($"No record with number {normalized} was found.");
    }

    [GeneratedRegex("^(?<prefix>[A-Z]{2,8})(?<digits>[0-9]{1,12})$")]
    private static partial Regex NumberPattern();
}
