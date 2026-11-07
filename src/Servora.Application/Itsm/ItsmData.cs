using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Events;
using Servora.Domain.Cmdb;
using Servora.Domain.Identity;

namespace Servora.Application.Itsm;

/// <summary>Shared persistence helpers of the ITSM services.</summary>
internal static class ItsmData
{
    /// <summary>Saves the unit of work and maps expected failures to results.</summary>
    /// <param name="db">The context.</param>
    /// <param name="recordLabel">The record label used in messages, for example <c>incident</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public static async Task<Result> SaveAsync(IAppDbContext db, string recordLabel, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (RecordSaveAbortedException ex)
        {
            return Result.Invalid(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict($"The {recordLabel} was changed by someone else in the meantime. Reload the record and apply your changes again.");
        }
    }

    /// <summary>Checks that referenced users, groups and configuration items exist.</summary>
    /// <param name="db">The context.</param>
    /// <param name="userIds">Referenced user ids (nulls are ignored).</param>
    /// <param name="groupId">The referenced group id.</param>
    /// <param name="ciIds">Referenced configuration item ids (nulls are ignored).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A user-safe error message, or <see langword="null"/> when all references exist.</returns>
    public static async Task<string?> ValidateReferencesAsync(IAppDbContext db, IEnumerable<Guid?> userIds, Guid? groupId, IEnumerable<Guid?> ciIds, CancellationToken cancellationToken)
    {
        var users = userIds.Where(u => u.HasValue).Select(u => u!.Value).Distinct().ToList();
        if (users.Count > 0 && await db.Set<User>().CountAsync(u => users.Contains(u.Id), cancellationToken) != users.Count)
        {
            return "A referenced user does not exist.";
        }

        if (groupId is { } group && !await db.Set<Group>().AnyAsync(g => g.Id == group, cancellationToken))
        {
            return "The assignment group does not exist.";
        }

        var cis = ciIds.Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();
        if (cis.Count > 0 && await db.Set<ConfigurationItem>().CountAsync(c => cis.Contains(c.Id), cancellationToken) != cis.Count)
        {
            return "A referenced configuration item does not exist.";
        }

        return null;
    }

    /// <summary>Normalizes free text: trims and converts empty strings to <see langword="null"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The normalized value.</returns>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
