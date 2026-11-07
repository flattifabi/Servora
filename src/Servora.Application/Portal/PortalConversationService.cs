using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Domain.Activity;
using Servora.Domain.Catalog;
using Servora.Domain.Common;

namespace Servora.Application.Portal;

/// <summary>
/// Default <see cref="IPortalConversationService"/> implementation. Ownership is verified here; reading and writing is
/// delegated to the kernel <see cref="IActivityService"/> (comments only) and <see cref="IAttachmentService"/>.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="activity">The activity stream service.</param>
/// <param name="attachments">The attachment service.</param>
public sealed class PortalConversationService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IActivityService activity,
    IAttachmentService attachments) : IPortalConversationService
{
    private const int MaxEntries = 300;
    private const int MaxRequestItems = 20;
    private const string NotFound = "The item was not found.";
    private const string Closed = "This item is closed. Please report a new issue or order again if you still need help.";

    /// <inheritdoc />
    public async Task<Result<PortalConversation>> GetAsync(PortalTicketKind kind, Guid recordId, CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalConversation>.Forbidden();
        }

        var target = await ResolveAsync(kind, recordId, me, cancellationToken);
        if (target is null)
        {
            return Result<PortalConversation>.NotFound(NotFound);
        }

        var entries = new List<ActivityEntry>();
        entries.AddRange(await activity.GetAsync(target.Table, recordId, includeWorkNotes: false, cancellationToken));
        foreach (var itemId in target.RequestItemIds)
        {
            // Approvals and fulfilment write their public events to the requested items of the request.
            entries.AddRange(await activity.GetAsync(TableNames.RequestItem, itemId, includeWorkNotes: false, cancellationToken));
        }

        var visible = entries
            .Where(e => e.Type is ActivityType.Comment or ActivityType.System && !string.IsNullOrWhiteSpace(e.Text))
            .OrderByDescending(e => e.CreatedAt)
            .Take(MaxEntries)
            .OrderBy(e => e.CreatedAt)
            .Select(e => ToEntry(e, me))
            .ToList();
        var files = (await attachments.ListAsync(target.Table, recordId, cancellationToken)).Select(ToAttachment).ToList();
        return Result<PortalConversation>.Success(new PortalConversation(visible, files, target.IsOpen));
    }

    /// <inheritdoc />
    public async Task<Result<PortalConversationEntry>> AddCommentAsync(PortalTicketKind kind, Guid recordId, string text, CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalConversationEntry>.Forbidden();
        }

        var target = await ResolveAsync(kind, recordId, me, cancellationToken);
        if (target is null)
        {
            return Result<PortalConversationEntry>.NotFound(NotFound);
        }

        if (!target.IsOpen)
        {
            return Result<PortalConversationEntry>.Invalid(Closed);
        }

        // Always a public comment – portal users can never write work notes.
        var added = await activity.AddAsync(target.Table, recordId, ActivityType.Comment, text ?? string.Empty, cancellationToken);
        return added.Succeeded && added.Value is { } entry
            ? Result<PortalConversationEntry>.Success(ToEntry(entry, me))
            : Result<PortalConversationEntry>.From(added);
    }

    /// <inheritdoc />
    public async Task<Result<PortalAttachment>> UploadAsync(PortalTicketKind kind, Guid recordId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalAttachment>.Forbidden();
        }

        var target = await ResolveAsync(kind, recordId, me, cancellationToken);
        if (target is null)
        {
            return Result<PortalAttachment>.NotFound(NotFound);
        }

        if (!target.IsOpen)
        {
            return Result<PortalAttachment>.Invalid(Closed);
        }

        var uploaded = await attachments.UploadAsync(target.Table, recordId, fileName, contentType, content, cancellationToken);
        return uploaded.Succeeded && uploaded.Value is { } attachment
            ? Result<PortalAttachment>.Success(ToAttachment(attachment))
            : Result<PortalAttachment>.From(uploaded);
    }

    /// <inheritdoc />
    public async Task<Result<(string FileName, Stream Content)>> OpenAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<(string, Stream)>.Forbidden();
        }

        Attachment? metadata;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            metadata = await db.Set<Attachment>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        }

        var kind = metadata?.TableName switch
        {
            TableNames.Incident => PortalTicketKind.Incident,
            TableNames.Request => PortalTicketKind.Request,
            _ => (PortalTicketKind?)null,
        };
        if (metadata is null || kind is null || await ResolveAsync(kind.Value, metadata.RecordId, me, cancellationToken) is null)
        {
            return Result<(string, Stream)>.NotFound("The file was not found.");
        }

        var opened = await attachments.OpenAsync(attachmentId, cancellationToken);
        return opened.Succeeded
            ? Result<(string, Stream)>.Success((opened.Value.Attachment.FileName, opened.Value.Content))
            : Result<(string, Stream)>.From(opened);
    }

    private static PortalConversationEntry ToEntry(ActivityEntry entry, Guid me) =>
        new(entry.Id, entry.Type == ActivityType.System, entry.AuthorName, entry.Type == ActivityType.Comment && entry.AuthorId == me, entry.Text ?? string.Empty, entry.CreatedAt);

    private static PortalAttachment ToAttachment(Attachment attachment) =>
        new(attachment.Id, attachment.FileName, attachment.SizeBytes, attachment.UploadedBy, attachment.UploadedAt);

    private async Task<ConversationTarget?> ResolveAsync(PortalTicketKind kind, Guid recordId, Guid me, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (kind == PortalTicketKind.Incident)
        {
            var incident = await PortalScope.OwnIncidents(db, me).Where(i => i.Id == recordId).Select(i => new { i.Active }).FirstOrDefaultAsync(cancellationToken);
            return incident is null ? null : new ConversationTarget(TableNames.Incident, incident.Active, []);
        }

        var request = await PortalScope.OwnRequests(db, me).Where(r => r.Id == recordId).Select(r => new { r.State }).FirstOrDefaultAsync(cancellationToken);
        if (request is null)
        {
            return null;
        }

        var itemIds = await db.Set<RequestItem>().AsNoTracking().Where(i => i.RequestId == recordId).OrderBy(i => i.Number).Select(i => i.Id).Take(MaxRequestItems).ToListAsync(cancellationToken);
        return new ConversationTarget(TableNames.Request, PortalScope.IsOpen(request.State), itemIds);
    }
}
