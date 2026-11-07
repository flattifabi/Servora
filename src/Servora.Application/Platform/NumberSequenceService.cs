using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Platform;

/// <summary>
/// Default <see cref="INumberSequenceService"/>. Uses optimistic concurrency on <see cref="NumberSequence.Version"/>
/// and retries on conflicts so numbers stay unique even with concurrent writers.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
public sealed class NumberSequenceService(IAppDbContextFactory dbFactory) : INumberSequenceService
{
    private const int MaxAttempts = 10;

    /// <inheritdoc />
    public async Task<string> NextAsync(string tableName, string defaultPrefix, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sequence = await db.Set<NumberSequence>().FirstOrDefaultAsync(s => s.TableName == tableName, cancellationToken);
            if (sequence is null)
            {
                sequence = new NumberSequence { TableName = tableName, Prefix = defaultPrefix };
                db.Set<NumberSequence>().Add(sequence);
            }

            var value = sequence.NextValue;
            sequence.NextValue++;
            sequence.Version++;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return sequence.Prefix + value.ToString(CultureInfo.InvariantCulture).PadLeft(sequence.Digits, '0');
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                await Task.Delay(Random.Shared.Next(5, 25) * attempt, cancellationToken);
            }
        }
    }
}
