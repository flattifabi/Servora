using Microsoft.EntityFrameworkCore;
using Servora.Application.Catalog;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Catalog;
using Servora.Domain.Identity;
using Servora.Tests.Unit.ServiceManagement;

namespace Servora.Tests.Unit.Catalog;

/// <summary>
/// Service tests of the catalog extensions for the portal: server-side pricing when ordering, favourites, order form
/// sections, product images and the authorization of the new methods (in-memory SQLite, real DI graph).
/// </summary>
public sealed class CatalogPortalServiceTests : IDisposable
{
    private static readonly byte[] s_png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];

    private readonly ServiceManagementTestHost _host = new();

    [Fact]
    public async Task Order_CalculatesThePriceServerSide_FromTheCatalogDefinition()
    {
        var itemId = await CreateLaptopAsync();
        var employee = await _host.AddUserAsync("Lea Mueller");
        _host.User.Become(employee, "Lea Mueller", Permissions.Portal.Access);

        var answers = new Dictionary<string, string?>
        {
            ["memory"] = "32 GB RAM",
            ["dock"] = "true",
            ["justification"] = "Presentations",
            ["price"] = "0.01",
            ["unit_price"] = "1",
        };
        var order = await _host.Get<ICatalogOrderService>().OrderAsync(itemId, answers, quantity: 2);
        Assert.True(order.Succeeded, order.Error);

        await using var db = _host.Database.CreateDbContext();
        var item = await db.Set<RequestItem>().SingleAsync(r => r.Id == order.Value!.RequestItemId);
        var request = await db.Set<Request>().SingleAsync(r => r.Id == order.Value!.RequestId);
        Assert.Equal(52m, item.UnitPrice);
        Assert.Equal(CatalogBillingType.Monthly, item.BillingType);
        Assert.Equal(36, item.TermMonths);
        Assert.Equal(10m, item.OneTimePrice);
        Assert.Equal(104m, request.TotalPrice);
        Assert.DoesNotContain("unit_price", item.VariablesJson, StringComparison.Ordinal);

        var quote = await _host.Get<ICatalogOrderService>().QuoteAsync(itemId, answers, 2);
        Assert.Equal(104m, quote.Value!.Total);
    }

    [Fact]
    public async Task OrderForm_ContainsSectionsPricingAndRequester()
    {
        var itemId = await CreateLaptopAsync();
        var manager = await _host.AddUserAsync("Jan Weber");
        var employee = await _host.AddUserAsync("Lea Mueller", manager);
        _host.User.Become(employee, "Lea Mueller", Permissions.Portal.Access);

        var form = (await _host.Get<ICatalogOrderService>().GetOrderFormAsync(itemId)).Value!;
        Assert.Equal(["Device & accessories", "Delivery & demand"], form.Sections.Select(s => s.Title));
        Assert.Equal(CatalogBillingType.Monthly, form.Pricing.BillingType);
        Assert.True(form.Item.HasPriceOptions);
        Assert.Equal("Jan Weber", form.ApproverName);
        Assert.False(form.CanOrderOnBehalf);
        Assert.Equal("Lea Mueller", form.RequesterName);
        Assert.Equal(2, form.Highlights.Count);
        Assert.Equal("truck", form.Highlights[0].Icon);
    }

    [Fact]
    public async Task Favorites_AreAddedRemovedAndPerUser()
    {
        var itemId = await CreateLaptopAsync();
        var lea = await _host.AddUserAsync("Lea Mueller");
        var bob = await _host.AddUserAsync("Bob Builder");
        var catalog = _host.Get<ICatalogOrderService>();

        _host.User.Become(lea, "Lea Mueller", Permissions.Portal.Access);
        Assert.True((await catalog.SetFavoriteAsync(itemId, true)).Succeeded);
        Assert.True((await catalog.SetFavoriteAsync(itemId, true)).Succeeded);
        Assert.Equal([itemId], (await catalog.GetFavoriteIdsAsync()).Value);
        Assert.True((await catalog.GetOrderFormAsync(itemId)).Value!.IsFavorite);
        Assert.Equal(ResultErrorKind.NotFound, (await catalog.SetFavoriteAsync(Guid.NewGuid(), true)).ErrorKind);

        _host.User.Become(bob, "Bob Builder", Permissions.Portal.Access);
        Assert.Empty((await catalog.GetFavoriteIdsAsync()).Value!);

        _host.User.Become(lea, "Lea Mueller", Permissions.Portal.Access);
        Assert.True((await catalog.SetFavoriteAsync(itemId, false)).Succeeded);
        Assert.Empty((await catalog.GetFavoriteIdsAsync()).Value!);

        _host.User.Become(lea, "Lea Mueller");
        Assert.Equal(ResultErrorKind.Forbidden, (await catalog.SetFavoriteAsync(itemId, true)).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await catalog.GetFavoriteIdsAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await catalog.QuoteAsync(itemId, new Dictionary<string, string?>())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await catalog.OpenImageAsync(Guid.NewGuid())).ErrorKind);
    }

    [Fact]
    public async Task NewAdminMethods_RequireCatalogManage()
    {
        var itemId = await CreateLaptopAsync();
        var user = await _host.AddUserAsync("Portal User");
        _host.User.Become(user, "Portal User", Permissions.Portal.Access, Permissions.Requests.Read);
        var admin = _host.Get<ICatalogAdminService>();

        Assert.Equal(ResultErrorKind.Forbidden, (await admin.SaveSectionAsync(itemId, new CatalogSectionModel { Title = "X" })).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await admin.DeleteSectionAsync(Guid.NewGuid())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await admin.UploadItemImageAsync(itemId, new MemoryStream(s_png))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await admin.RemoveItemImageAsync(itemId)).ErrorKind);
    }

    [Fact]
    public async Task DeletingASection_MovesItsQuestionsToTheGeneralSection()
    {
        var itemId = await CreateLaptopAsync();
        _host.User.Become(Guid.NewGuid(), "Catalog Admin", Permissions.Requests.ManageCatalog);
        var admin = _host.Get<ICatalogAdminService>();
        var model = (await admin.GetItemAsync(itemId)).Value!;
        var device = model.Sections.First();

        Assert.True((await admin.DeleteSectionAsync(device.Id!.Value)).Succeeded);
        model = (await admin.GetItemAsync(itemId)).Value!;
        Assert.Single(model.Sections);
        Assert.All(model.Variables.Where(v => v.Name is "memory" or "dock"), v => Assert.Null(v.SectionId));

        var other = await admin.CreateItemAsync(new CatalogItemModel { Name = "Other" });
        var foreignSection = await admin.SaveSectionAsync(other.Value, new CatalogSectionModel { Title = "Foreign" });
        var variable = CatalogVariableModel.From(model.Variables.First(v => v.Name == "memory"));
        variable.SectionId = foreignSection.Value;
        Assert.Equal(ResultErrorKind.Validation, (await admin.SaveVariableAsync(itemId, variable)).ErrorKind);
    }

    [Fact]
    public async Task ItemImages_AreTypeChecked_AndOnlyServedForActiveItems()
    {
        var itemId = await CreateLaptopAsync();
        _host.User.Become(Guid.NewGuid(), "Catalog Admin", Permissions.Requests.ManageCatalog);
        var admin = _host.Get<ICatalogAdminService>();

        Assert.Equal(ResultErrorKind.Validation, (await admin.UploadItemImageAsync(itemId, new MemoryStream("<svg onload=alert(1)>"u8.ToArray()))).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await admin.UploadItemImageAsync(itemId, new MemoryStream(new byte[ICatalogImageStore.MaxImageBytes + 1]))).ErrorKind);
        Assert.True((await admin.UploadItemImageAsync(itemId, new MemoryStream(s_png))).Succeeded);
        var imageId = (await admin.GetItemAsync(itemId)).Value!.ImageId!.Value;

        var user = await _host.AddUserAsync("Portal User");
        _host.User.Become(user, "Portal User", Permissions.Portal.Access);
        var catalog = _host.Get<ICatalogOrderService>();
        var image = await catalog.OpenImageAsync(imageId);
        Assert.True(image.Succeeded);
        Assert.Equal("image/png", image.Value!.ContentType);
        await image.Value.Content.DisposeAsync();
        Assert.Equal(ResultErrorKind.NotFound, (await catalog.OpenImageAsync(Guid.NewGuid())).ErrorKind);

        await using (var db = _host.Database.CreateDbContext())
        {
            var item = await db.Set<CatalogItem>().SingleAsync(i => i.Id == itemId);
            item.IsActive = false;
            await db.SaveChangesAsync();
        }

        Assert.Equal(ResultErrorKind.NotFound, (await catalog.OpenImageAsync(imageId)).ErrorKind);

        _host.User.Become(Guid.NewGuid(), "Catalog Admin", Permissions.Requests.ManageCatalog);
        Assert.True((await admin.RemoveItemImageAsync(itemId)).Succeeded);
        Assert.Null((await admin.GetItemAsync(itemId)).Value!.ImageId);
    }

    [Fact]
    public async Task Duplicate_CopiesSectionsAndPresentation()
    {
        var itemId = await CreateLaptopAsync();
        _host.User.Become(Guid.NewGuid(), "Catalog Admin", Permissions.Requests.ManageCatalog);
        var admin = _host.Get<ICatalogAdminService>();
        var copyId = (await admin.DuplicateItemAsync(itemId)).Value;
        var copy = (await admin.GetItemAsync(copyId)).Value!;

        Assert.Equal(2, copy.Sections.Count);
        Assert.Equal(CatalogBillingType.Monthly, copy.BillingType);
        Assert.Equal("Lenovo ThinkPad T14 Gen 5", copy.Subtitle);
        Assert.Null(copy.Code);
        var memory = copy.Variables.Single(v => v.Name == "memory");
        Assert.Contains(copy.Sections, s => s.Id == memory.SectionId);
        Assert.Equal(8m, memory.Choices.Single(c => c.Label == "32 GB RAM").PriceDelta);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private async Task<Guid> CreateLaptopAsync()
    {
        var approver = await _host.AddUserAsync("Ann Approver");
        var approvers = await _host.AddGroupAsync("Approvers", GroupType.Approval, approver);
        var item = new CatalogItem
        {
            Name = "Business laptop", Subtitle = "Lenovo ThinkPad T14 Gen 5", Code = "HW-001",
            Price = 39m, BillingType = CatalogBillingType.Monthly, TermMonths = 36, OneTimePrice = 10m,
            Highlights = "truck | 3–5 working days\nIT support included",
            ApprovalRequired = true, ManagerApproval = true, ApprovalGroupId = approvers,
        };
        var device = new CatalogSection { CatalogItemId = item.Id, Title = "Device & accessories", Order = 10 };
        var delivery = new CatalogSection { CatalogItemId = item.Id, Title = "Delivery & demand", Order = 20 };
        await using var db = _host.Database.CreateDbContext();
        db.Set<CatalogItem>().Add(item);
        db.Set<CatalogSection>().AddRange(device, delivery);
        db.Set<CatalogVariable>().AddRange(
            new CatalogVariable { CatalogItemId = item.Id, SectionId = device.Id, Name = "memory", Label = "Memory", Type = CatalogVariableType.Choice, Mandatory = true, Options = "16 GB RAM | 0\n32 GB RAM | +8.00", DefaultValue = "16 GB RAM", Order = 10 },
            new CatalogVariable { CatalogItemId = item.Id, SectionId = device.Id, Name = "dock", Label = "Docking station", Type = CatalogVariableType.Boolean, PriceDelta = 5m, Order = 20 },
            new CatalogVariable { CatalogItemId = item.Id, SectionId = delivery.Id, Name = "justification", Label = "Justification", Type = CatalogVariableType.MultilineText, Mandatory = true, Order = 30 });
        await db.SaveChangesAsync();
        return item.Id;
    }
}
