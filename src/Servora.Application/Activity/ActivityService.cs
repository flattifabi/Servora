using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Activity;

namespace Servora.Application.Activity;

/// <summary>Default <see cref="IActivityService"/> implementation backed by the <see cref="ActivityEntry"/> table.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
public sealed class ActivityService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IClock clock) : IActivityService
{
    private const int MaxTextLength = 20_000;
    private const int MaxEntries = 500;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityEntry>> GetAsync(string tableName, Guid recordId, bool includeWorkNotes, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<ActivityEntry>().AsNoTracking().Where(a => a.TableName == tableName && a.RecordId == recordId);
        if (!includeWorkNotes)
        {
            query = query.Where(a => a.Type == ActivityType.Comment || a.Type == ActivityType.System);
        }

        var entries = await query.ToListAsync(cancellationToken);
        return entries.OrderByDescending(a => a.CreatedAt).Take(MaxEntries).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<ActivityEntry>> AddAsync(string tableName, Guid recordId, ActivityType type, string text, CancellationToken cancellationToken = default)
    {
        if (type is not (ActivityType.Comment or ActivityType.WorkNote))
        {
            return Result<ActivityEntry>.Invalid("Only comments and work notes can be added manually.");
        }

        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return Result<ActivityEntry>.Invalid("The text must not be empty.");
        }

        if (text.Length > MaxTextLength)
        {
            return Result<ActivityEntry>.Invalid($"The text must not exceed {MaxTextLength} characters.");
        }

        var entry = new ActivityEntry
        {
            TableName = tableName,
            RecordId = recordId,
            Type = type,
            Text = text,
            AuthorId = currentUser.UserId,
            AuthorName = currentUser.DisplayName,
            CreatedAt = clock.UtcNow,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<ActivityEntry>().Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return Result<ActivityEntry>.Success(entry);
    }

    /// <inheritdoc />
    public async Task AddSystemAsync(string tableName, Guid recordId, string text, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<ActivityEntry>().Add(new ActivityEntry
        {
            TableName = tableName,
            RecordId = recordId,
            Type = ActivityType.System,
            Text = text.Length > MaxTextLength ? text[..MaxTextLength] : text,
            AuthorName = currentUser.DisplayName,
            AuthorId = currentUser.UserId,
            CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
