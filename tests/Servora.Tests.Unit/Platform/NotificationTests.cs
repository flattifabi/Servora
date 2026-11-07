using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Notifications;
using Servora.Application.Platform;
using Servora.Application.Tables;
using Servora.Domain.Common;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.Platform;

/// <summary>Tests for notification rendering, triggers, recipients and the in-app inbox.</summary>
public sealed class NotificationTests : IAsyncLifetime
{
    private const string Table = "u_request";
    private PlatformTestHost _host = null!;
    private Guid _requesterId;

    public async Task InitializeAsync()
    {
        _host = await PlatformTestHost.CreateAsync();
        _requesterId = await _host.DbAsync(async db =>
        {
            var user = new User { UserName = "bob", NormalizedUserName = "BOB", DisplayName = "Bob <Builder>", Email = "bob@example.com", PasswordHash = "hash" };
            db.Set<User>().Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });
        await _host.RunAsync<ITableDesignerService>(async designer =>
        {
            Assert.True((await designer.CreateTableAsync(new CustomTableInput { Name = Table, Label = "Request", NumberPrefix = "RQ" })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_title", Label = "Title", Type = FieldType.String })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_requester", Label = "Requester", Type = FieldType.Reference, ReferenceTable = TableNames.User })).Succeeded);
            Assert.True((await designer.AddFieldAsync(Table, new CustomFieldInput { Name = "u_urgent", Label = "Urgent", Type = FieldType.Boolean })).Succeeded);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public void Renderer_EncodesValuesAndStripsLineBreaksFromSubject()
    {
        var values = new Dictionary<string, object?>
        {
            ["Title"] = "<script>alert(1)</script>",
            ["Requester"] = Guid.NewGuid(),
            ["Requester.display"] = "Eve & Co",
            ["link"] = "https://servora.example.com/table/x/1",
        };
        var message = NotificationRenderer.Render("New: ${Title}\r\nBcc: attacker@example.com", "Hello ${Requester},\n${Title} <b>${Unknown}</b>", values);

        Assert.DoesNotContain('\n', message.Subject);
        Assert.DoesNotContain('\r', message.Subject);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", message.HtmlBody);
        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("Eve &amp; Co", message.HtmlBody);
        Assert.Contains("&lt;b&gt;&lt;/b&gt;", message.HtmlBody);
        Assert.Contains("<br>", message.HtmlBody);
        Assert.Contains("href=\"https://servora.example.com/table/x/1\"", message.HtmlBody);
        Assert.StartsWith("Hello Eve & Co,", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trigger_SendsEmailAndInAppNotification()
    {
        var saved = await _host.RunAsync<INotificationAdminService, Result<Guid>>(s => s.SaveAsync(new NotificationModel
        {
            Name = "Urgent request",
            TableName = Table,
            OnInsert = true,
            Condition = new ConditionGroup { Rules = [new ConditionRule { Field = "u_urgent", Operator = ConditionOperator.Equals, Value = "true" }] },
            Subject = "Urgent: ${Number} ${u_title}",
            Body = "Requested by ${u_requester}.",
            RecipientFields = ["u_requester"],
            RecipientEmails = "desk@example.com",
        }));
        Assert.True(saved.Succeeded, saved.Error);

        await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "Normal", ["u_requester"] = _requesterId }));
        await _host.DispatchEventsAsync();
        Assert.Empty(_host.Emails.Messages);

        await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "VPN down", ["u_requester"] = _requesterId, ["u_urgent"] = true }));
        await _host.DispatchEventsAsync();

        var messages = _host.Emails.Messages.ToList();
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Single(m.To));
        Assert.Contains(messages, m => m.To[0] == "bob@example.com");
        Assert.Contains(messages, m => m.To[0] == "desk@example.com");
        Assert.Equal("Urgent: RQ0001002 VPN down", messages[0].Subject);
        Assert.Contains("Requested by Bob &lt;Builder&gt;.", messages[0].HtmlBody);

        _host.Identity.UserId = _requesterId;
        var inbox = await _host.RunAsync<INotificationInbox, PagedResult<InboxItem>>(i => i.GetAsync(new PageRequest()));
        var item = Assert.Single(inbox.Items);
        Assert.Equal("Urgent: RQ0001002 VPN down", item.Title);
        Assert.StartsWith($"/table/{Table}/", item.Link, StringComparison.Ordinal);
        Assert.Equal(1, await _host.RunAsync<INotificationInbox, int>(i => i.GetUnreadCountAsync()));

        Assert.True((await _host.RunAsync<INotificationInbox, Result>(i => i.MarkReadAsync(item.Id))).Succeeded);
        Assert.Equal(0, await _host.RunAsync<INotificationInbox, int>(i => i.GetUnreadCountAsync()));

        // Other users never see the message.
        _host.Identity.UserId = Guid.NewGuid();
        Assert.Empty((await _host.RunAsync<INotificationInbox, PagedResult<InboxItem>>(i => i.GetAsync(new PageRequest()))).Items);
        Assert.Equal(ResultErrorKind.NotFound, (await _host.RunAsync<INotificationInbox, Result>(i => i.MarkReadAsync(item.Id))).ErrorKind);
    }

    [Fact]
    public async Task Templates_NeverExposeSecrets()
    {
        var saved = await _host.RunAsync<INotificationAdminService, Result<Guid>>(s => s.SaveAsync(new NotificationModel
        {
            Name = "User changed",
            TableName = TableNames.User,
            OnInsert = false,
            OnUpdate = true,
            Subject = "User ${UserName}",
            Body = "Hash: ${PasswordHash} Stamp: ${SecurityStamp}",
            RecipientEmails = "audit@example.com",
        }));
        Assert.True(saved.Succeeded, saved.Error);

        await _host.RunAsync<IRecordService, Result>(s => s.UpdateAsync(TableNames.User, _requesterId, new Dictionary<string, object?> { ["Title"] = "Engineer" }));
        await _host.DispatchEventsAsync();
        var message = Assert.Single(_host.Emails.Messages);
        Assert.Equal("User bob", message.Subject);
        Assert.DoesNotContain("hash", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Hash:  Stamp: ", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validation_AndPreview()
    {
        var noRecipients = await _host.RunAsync<INotificationAdminService, Result<Guid>>(s => s.SaveAsync(new NotificationModel { Name = "x", TableName = Table, Subject = "s", Body = "b" }));
        Assert.Contains("recipient", noRecipients.Error);

        var badField = await _host.RunAsync<INotificationAdminService, Result<Guid>>(s => s.SaveAsync(new NotificationModel { Name = "x", TableName = Table, Subject = "s", Body = "b", RecipientFields = ["u_title"] }));
        Assert.Contains("not a reference", badField.Error);

        var badEmail = await _host.RunAsync<INotificationAdminService, Result<Guid>>(s => s.SaveAsync(new NotificationModel { Name = "x", TableName = Table, Subject = "s", Body = "b", RecipientEmails = "a@b.com, nope" }));
        Assert.Contains("nope", badEmail.Error);

        await _host.RunAsync<IRecordService, Result<Guid>>(s => s.CreateAsync(Table, new Dictionary<string, object?> { ["u_title"] = "Preview me", ["u_requester"] = _requesterId }));
        var preview = await _host.RunAsync<INotificationAdminService, Result<RenderedNotification>>(s => s.PreviewAsync(new NotificationModel { TableName = Table, Subject = "${u_title} for ${u_requester}", Body = "x" }, null));
        Assert.True(preview.Succeeded, preview.Error);
        Assert.Equal("Preview me for Bob <Builder>", preview.Value!.Subject);
    }
}
