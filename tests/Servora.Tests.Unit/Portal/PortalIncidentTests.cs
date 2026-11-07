using System.Text;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Itsm;
using Servora.Application.Portal;
using Servora.Application.Security;
using Servora.Domain.Activity;
using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.Portal;

/// <summary>
/// Portal issues (incidents): reporting as caller, own-record visibility, comments without work notes, attachments and the
/// resolution confirmation – and proof that other users can neither see nor change someone else's issue.
/// </summary>
public sealed class PortalIncidentTests : IDisposable
{
    private readonly PortalTestHost _host = new();

    [Fact]
    public async Task PortalOnlyUser_ReportsIssue_AsCaller_ViaPortalChannel_WithCalculatedPriority()
    {
        var anna = await _host.AddUserAsync("Anna Employee");
        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var tickets = _host.Get<IPortalTicketService>();

        var created = await tickets.ReportIssueAsync(new PortalIssueInput
        {
            ShortDescription = "Outlook does not start",
            Description = "Crashes on start since this morning.",
            Urgency = Urgency.High,
            Impact = Impact.High,
        });

        Assert.True(created.Succeeded, created.Error);
        Assert.StartsWith("INC", created.Value!.Number, StringComparison.Ordinal);
        await using var db = _host.Create();
        var incident = await db.Set<Incident>().SingleAsync(i => i.Id == created.Value.Id);
        Assert.Equal(anna, incident.CallerId);
        Assert.Equal(anna, incident.OpenedById);
        Assert.Equal(ContactChannel.Portal, incident.Channel);
        Assert.Equal(IncidentState.New, incident.State);
        Assert.Equal(Priority.Critical, incident.Priority);

        var list = await tickets.ListAsync(new PortalTicketQuery());
        var row = Assert.Single(list.Value!.Items);
        Assert.Equal(PortalTicketKind.Incident, row.Kind);
        Assert.Equal("Submitted", row.Status.Label);

        var detail = await tickets.GetIncidentAsync(created.Value.Id);
        Assert.True(detail.Succeeded, detail.Error);
        Assert.Equal(PortalStepState.Current, detail.Value!.Steps[0].State);
        Assert.True(detail.Value.CanComment);
    }

    [Fact]
    public async Task ReportIssue_RejectsInvalidInput_AndUnknownCategory()
    {
        var anna = await _host.AddUserAsync("Anna Employee");
        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var tickets = _host.Get<IPortalTicketService>();

        var tooShort = await tickets.ReportIssueAsync(new PortalIssueInput { ShortDescription = "x" });
        Assert.Equal(ResultErrorKind.Validation, tooShort.ErrorKind);

        var badCategory = await tickets.ReportIssueAsync(new PortalIssueInput { ShortDescription = "Printer is broken", Category = "Not a category" });
        Assert.Equal(ResultErrorKind.Validation, badCategory.ErrorKind);
    }

    [Fact]
    public async Task OtherUser_CannotSeeListCommentUploadOrChange_SomeoneElsesIssue()
    {
        var anna = await _host.AddUserAsync("Anna Employee");
        var mallory = await _host.AddUserAsync("Mallory Other");
        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var issue = (await _host.Get<IPortalTicketService>().ReportIssueAsync(new PortalIssueInput { ShortDescription = "VPN keeps disconnecting" })).Value!;
        await using (var upload = new MemoryStream(Encoding.UTF8.GetBytes("log")))
        {
            Assert.True((await _host.Get<IPortalConversationService>().UploadAsync(PortalTicketKind.Incident, issue.Id, "vpn.log", "text/plain", upload)).Succeeded);
        }

        var attachmentId = (await _host.Get<IPortalConversationService>().GetAsync(PortalTicketKind.Incident, issue.Id)).Value!.Attachments.Single().Id;

        // Mallory holds portal.access too – but the issue is not hers.
        _host.User.Become(mallory, "Mallory Other", Permissions.Portal.Access);
        var tickets = _host.Get<IPortalTicketService>();
        var conversations = _host.Get<IPortalConversationService>();

        Assert.Empty((await tickets.ListAsync(new PortalTicketQuery(PortalTicketFilter.All))).Value!.Items);
        Assert.Equal(ResultErrorKind.NotFound, (await tickets.GetIncidentAsync(issue.Id)).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await conversations.GetAsync(PortalTicketKind.Incident, issue.Id)).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await conversations.AddCommentAsync(PortalTicketKind.Incident, issue.Id, "I can see this")).ErrorKind);
        await using (var upload = new MemoryStream([1, 2, 3]))
        {
            Assert.Equal(ResultErrorKind.NotFound, (await conversations.UploadAsync(PortalTicketKind.Incident, issue.Id, "x.txt", "text/plain", upload)).ErrorKind);
        }

        Assert.Equal(ResultErrorKind.NotFound, (await conversations.OpenAttachmentAsync(attachmentId)).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await tickets.AcceptResolutionAsync(issue.Id)).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await tickets.ReopenIncidentAsync(issue.Id, "reopen")).ErrorKind);

        // Searching by number does not reveal it either.
        var search = await _host.Get<IPortalHomeService>().SearchAsync(issue.Number);
        Assert.Empty(search.Value!.MyItems);

        await using var db = _host.Create();
        Assert.False(await db.Set<ActivityEntry>().AnyAsync(a => a.RecordId == issue.Id && a.AuthorId == mallory));
    }

    [Fact]
    public async Task Conversation_ShowsCommentsAndSystemEvents_ButNeverWorkNotes()
    {
        var anna = await _host.AddUserAsync("Anna Employee");
        var fiona = await _host.AddUserAsync("Fiona Fulfiller");
        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var issue = (await _host.Get<IPortalTicketService>().ReportIssueAsync(new PortalIssueInput { ShortDescription = "Laptop screen flickers" })).Value!;
        var posted = await _host.Get<IPortalConversationService>().AddCommentAsync(PortalTicketKind.Incident, issue.Id, "It started after the update.");
        Assert.True(posted.Succeeded, posted.Error);
        Assert.True(posted.Value!.IsMine);

        _host.User.Become(fiona, "Fiona Fulfiller", Permissions.Incidents.Read, Permissions.Incidents.Write);
        await _host.Get<IActivityService>().AddAsync(TableNames.Incident, issue.Id, ActivityType.WorkNote, "Internal: probably the GPU driver.");
        await _host.Get<IActivityService>().AddAsync(TableNames.Incident, issue.Id, ActivityType.Comment, "Please restart and tell us if it persists.");

        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var conversation = (await _host.Get<IPortalConversationService>().GetAsync(PortalTicketKind.Incident, issue.Id)).Value!;
        Assert.DoesNotContain(conversation.Entries, e => e.Text.Contains("Internal", StringComparison.Ordinal));
        Assert.Contains(conversation.Entries, e => !e.IsSystem && !e.IsMine && e.AuthorName == "Fiona Fulfiller");
        Assert.Contains(conversation.Entries, e => e.IsSystem);
        Assert.True(conversation.CanPost);

        await using var db = _host.Create();
        var annaEntries = await db.Set<ActivityEntry>().Where(a => a.RecordId == issue.Id && a.AuthorId == anna && a.Type != ActivityType.System).ToListAsync();
        Assert.All(annaEntries, e => Assert.Equal(ActivityType.Comment, e.Type));
    }

    [Fact]
    public async Task Attachments_OwnerCanUploadAndDownload()
    {
        var anna = await _host.AddUserAsync("Anna Employee");
        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var issue = (await _host.Get<IPortalTicketService>().ReportIssueAsync(new PortalIssueInput { ShortDescription = "Error message in SAP" })).Value!;
        var conversations = _host.Get<IPortalConversationService>();
        await using (var upload = new MemoryStream(Encoding.UTF8.GetBytes("screenshot")))
        {
            var uploaded = await conversations.UploadAsync(PortalTicketKind.Incident, issue.Id, "error.png", "image/png", upload);
            Assert.True(uploaded.Succeeded, uploaded.Error);
            Assert.StartsWith("/portal/attachments/", uploaded.Value!.DownloadUrl, StringComparison.Ordinal);

            var opened = await conversations.OpenAttachmentAsync(uploaded.Value.Id);
            Assert.True(opened.Succeeded, opened.Error);
            await using var content = opened.Value.Content;
            using var reader = new StreamReader(content);
            Assert.Equal("screenshot", await reader.ReadToEndAsync());
        }
    }

    [Fact]
    public async Task Caller_ConfirmsResolution_OrReopens_WithComment()
    {
        var anna = await _host.AddUserAsync("Anna Employee");
        var fiona = await _host.AddUserAsync("Fiona Fulfiller");
        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var first = (await _host.Get<IPortalTicketService>().ReportIssueAsync(new PortalIssueInput { ShortDescription = "Teams has no sound" })).Value!;
        var second = (await _host.Get<IPortalTicketService>().ReportIssueAsync(new PortalIssueInput { ShortDescription = "Printer on floor 3 jams" })).Value!;

        _host.User.Become(fiona, "Fiona Fulfiller", Permissions.Incidents.Read, Permissions.Incidents.Write);
        var incidents = _host.Get<IIncidentService>();
        Assert.True((await incidents.ResolveAsync(first.Id, IncidentResolutionCode.SolvedRemotely, "Audio driver reinstalled.")).Succeeded);
        Assert.True((await incidents.ResolveAsync(second.Id, IncidentResolutionCode.SolvedPermanently, "Roller replaced.")).Succeeded);

        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var tickets = _host.Get<IPortalTicketService>();
        var detail = (await tickets.GetIncidentAsync(first.Id)).Value!;
        Assert.True(detail.CanAcceptResolution);
        Assert.True(detail.Status.NeedsAction);
        Assert.Equal("Audio driver reinstalled.", detail.ResolutionNotes);

        Assert.True((await tickets.AcceptResolutionAsync(first.Id)).Succeeded);
        Assert.False((await tickets.GetIncidentAsync(first.Id)).Value!.CanComment);
        Assert.Equal(ResultErrorKind.Validation, (await _host.Get<IPortalConversationService>().AddCommentAsync(PortalTicketKind.Incident, first.Id, "late")).ErrorKind);

        Assert.Equal(ResultErrorKind.Validation, (await tickets.ReopenIncidentAsync(second.Id, " ")).ErrorKind);
        Assert.True((await tickets.ReopenIncidentAsync(second.Id, "Still jams every second page.")).Succeeded);

        await using var db = _host.Create();
        var closed = await db.Set<Incident>().SingleAsync(i => i.Id == first.Id);
        Assert.Equal(IncidentState.Closed, closed.State);
        var reopened = await db.Set<Incident>().SingleAsync(i => i.Id == second.Id);
        Assert.Equal(IncidentState.InProgress, reopened.State);
        Assert.Equal(1, reopened.ReopenCount);
        Assert.True(await db.Set<ActivityEntry>().AnyAsync(a => a.RecordId == second.Id && a.Type == ActivityType.Comment && a.Text == "Still jams every second page."));
    }

    [Fact]
    public async Task WithoutPortalAccess_EverythingIsForbidden()
    {
        var nobody = await _host.AddUserAsync("No Access");
        _host.User.Become(nobody, "No Access");
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<IPortalTicketService>().ListAsync(new PortalTicketQuery())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<IPortalTicketService>().ReportIssueAsync(new PortalIssueInput { ShortDescription = "Something broke" })).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<IPortalConversationService>().GetAsync(PortalTicketKind.Incident, Guid.NewGuid())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<IPortalHomeService>().GetHomeAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<IPortalKnowledgeService>().SearchAsync("vpn")).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<IPortalApprovalService>().GetSubjectAsync(Guid.NewGuid())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<IPortalAnnouncementService>().GetLiveAsync()).ErrorKind);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
