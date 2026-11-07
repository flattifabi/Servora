using Servora.Application.Common;
using Servora.Application.Portal;
using Servora.Application.Security;
using Servora.Domain.Portal;

namespace Servora.Tests.Unit.Portal;

/// <summary>Portal announcements: display window, ordering, administration permission, link validation and auditing.</summary>
public sealed class PortalAnnouncementTests : IDisposable
{
    private readonly PortalTestHost _host = new();

    [Fact]
    public async Task LiveAnnouncements_RespectWindowAndActiveFlag_OutagesFirst()
    {
        var admin = await _host.AddUserAsync("Paula Portal");
        _host.User.Become(admin, "Paula Portal", Permissions.Portal.Access, Permissions.Portal.Manage);
        var service = _host.Get<IPortalAnnouncementService>();
        var now = _host.Clock.UtcNow;

        await SaveAsync(service, "Welcome", PortalAnnouncementKind.Info, now.AddHours(-1), null, true, 1);
        await SaveAsync(service, "Email outage", PortalAnnouncementKind.Outage, now.AddMinutes(-5), now.AddHours(2), true, 50);
        await SaveAsync(service, "Future maintenance", PortalAnnouncementKind.Maintenance, now.AddDays(2), null, true, 0);
        await SaveAsync(service, "Expired", PortalAnnouncementKind.Info, now.AddDays(-3), now.AddDays(-1), true, 0);
        await SaveAsync(service, "Draft", PortalAnnouncementKind.Info, now.AddDays(-3), null, false, 0);
        Assert.Equal(5, _host.Audit.Entries.Count(e => e.Action == "announcement.created"));

        var employee = await _host.AddUserAsync("Anna Employee");
        _host.User.Become(employee, "Anna Employee", Permissions.Portal.Access);
        var live = (await service.GetLiveAsync()).Value!;
        Assert.Equal(["Email outage", "Welcome"], live.Select(a => a.Title).ToArray());

        // Employees cannot administrate announcements.
        Assert.Equal(ResultErrorKind.Forbidden, (await service.ListAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.SaveAsync(null, new PortalAnnouncementInput { Title = "Hack", StartsAt = now })).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.DeleteAsync(live[0].Id)).ErrorKind);
    }

    [Fact]
    public async Task Save_RejectsUnsafeLinks_AndDetectsConcurrentEdits()
    {
        var admin = await _host.AddUserAsync("Paula Portal");
        _host.User.Become(admin, "Paula Portal", Permissions.Portal.Access, Permissions.Portal.Manage);
        var service = _host.Get<IPortalAnnouncementService>();
        var now = _host.Clock.UtcNow;

        var unsafeLink = await service.SaveAsync(null, new PortalAnnouncementInput { Title = "Click", StartsAt = now, LinkUrl = "javascript:alert(1)" });
        Assert.Equal(ResultErrorKind.Validation, unsafeLink.ErrorKind);

        var created = await service.SaveAsync(null, new PortalAnnouncementInput { Title = "Maintenance", StartsAt = now, LinkUrl = "/portal/knowledge", LinkText = "Details" });
        Assert.True(created.Succeeded, created.Error);
        var stored = Assert.Single((await service.ListAsync()).Value!);
        Assert.Equal("/portal/knowledge", stored.LinkUrl);

        var update = new PortalAnnouncementInput { Title = "Maintenance tonight", StartsAt = now, Version = stored.Version };
        Assert.True((await service.SaveAsync(stored.Id, update)).Succeeded);
        Assert.Equal(ResultErrorKind.Conflict, (await service.SaveAsync(stored.Id, update)).ErrorKind);

        Assert.True((await service.DeleteAsync(stored.Id)).Succeeded);
        Assert.Empty((await service.ListAsync()).Value!);
        Assert.Contains(_host.Audit.Entries, e => e.Action == "announcement.deleted");
    }

    private static async Task SaveAsync(IPortalAnnouncementService service, string title, PortalAnnouncementKind kind, DateTimeOffset start, DateTimeOffset? end, bool active, int order)
    {
        var result = await service.SaveAsync(null, new PortalAnnouncementInput { Title = title, Kind = kind, StartsAt = start, EndsAt = end, IsActive = active, Order = order });
        Assert.True(result.Succeeded, result.Error);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
