using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Notifications;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Application.Sla;
using Servora.Application.Tables;
using Servora.Domain.Catalog;
using Servora.Domain.Identity;
using Servora.Domain.Knowledge;
using Servora.Infrastructure.Activity;
using Servora.Infrastructure.Modules;
using Servora.Infrastructure.Persistence;
using Servora.Infrastructure.Placeholders;
using Servora.Infrastructure.Platform;

namespace Servora.Tests.Unit.Portal;

/// <summary>
/// In-memory SQLite host for portal tests: the real DI graphs of the ITSM, service management, knowledge and portal
/// modules, saving hooks inside the context, a file-system attachment store in a temporary folder and a switchable actor.
/// </summary>
public sealed class PortalTestHost : IAppDbContextFactory, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ServoraDbContext> _options;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    /// <summary>Initializes a new instance of the <see cref="PortalTestHost"/> class.</summary>
    public PortalTestHost()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(_connection).Options;
        using (var db = new ServoraDbContext(_options))
        {
            db.Database.EnsureCreated();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAppDbContextFactory>(this);
        services.AddSingleton<ICurrentUser>(User);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IAuditService>(Audit);
        services.AddSingleton<IAppPaths>(Paths);
        services.AddSingleton<IEmailSender, NullEmailSender>();
        services.AddSingleton<ITableRegistry, ReflectionTableRegistry>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IDirectoryLookupService, DirectoryLookupService>();
        services.AddScoped<ISlaService, EmptySlaService>();
        services.AddItsmModule();
        services.AddServiceManagementModule();
        services.AddKnowledgeCmdbModule();
        services.AddPortalModule();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    /// <summary>Gets the switchable actor.</summary>
    public PortalTestUser User { get; } = new();

    /// <summary>Gets the clock.</summary>
    public PortalTestClock Clock { get; } = new();

    /// <summary>Gets the recorded audit entries.</summary>
    public PortalTestAudit Audit { get; } = new();

    /// <summary>Gets the temporary data folders.</summary>
    public PortalTestPaths Paths { get; } = new();

    /// <summary>Resolves a service from the test scope.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service.</returns>
    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Creates a context with the saving hooks of the registered modules.</summary>
    /// <returns>The context.</returns>
    public ServoraDbContext Create() => new(_options)
    {
        Services = _scope.ServiceProvider,
        ActorName = User.DisplayName,
        ActorUserId = User.UserId,
    };

    /// <inheritdoc />
    public IAppDbContext CreateDbContext() => Create();

    /// <inheritdoc />
    public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAppDbContext>(Create());

    /// <summary>Creates an active user.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>The user identifier.</returns>
    public async Task<Guid> AddUserAsync(string name)
    {
        var login = name.Replace(" ", ".", StringComparison.Ordinal).ToLowerInvariant();
        var user = new User { UserName = login, NormalizedUserName = login.ToUpperInvariant(), DisplayName = name, Email = login + "@example.test" };
        await using var db = Create();
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Creates a group with members.</summary>
    /// <param name="name">The group name.</param>
    /// <param name="type">The group type.</param>
    /// <param name="members">The members.</param>
    /// <returns>The group identifier.</returns>
    public async Task<Guid> AddGroupAsync(string name, GroupType type, params Guid[] members)
    {
        var group = new Group { Name = name, Type = type };
        foreach (var member in members)
        {
            group.Members.Add(new GroupMember { GroupId = group.Id, UserId = member });
        }

        await using var db = Create();
        db.Set<Group>().Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    /// <summary>Creates an active catalog item with one mandatory text variable <c>reason</c>.</summary>
    /// <param name="name">The item name.</param>
    /// <param name="approvalGroupId">An optional approval group (makes the item require approval).</param>
    /// <param name="fulfilmentGroupId">The fulfilment group.</param>
    /// <returns>The catalog item identifier.</returns>
    public async Task<Guid> AddCatalogItemAsync(string name, Guid? approvalGroupId, Guid? fulfilmentGroupId)
    {
        var item = new CatalogItem
        {
            Name = name,
            ShortDescription = $"{name} for employees",
            Icon = "laptop",
            Price = 99.5m,
            DeliveryDays = 2,
            FulfillmentGroupId = fulfilmentGroupId,
            ApprovalRequired = approvalGroupId is not null,
            ApprovalGroupId = approvalGroupId,
            IsActive = true,
        };
        await using var db = Create();
        db.Set<CatalogItem>().Add(item);
        db.Set<CatalogVariable>().Add(new CatalogVariable
        {
            CatalogItemId = item.Id,
            Name = "reason",
            Label = "Business reason",
            Type = CatalogVariableType.Text,
            Mandatory = true,
            Order = 10,
        });
        await db.SaveChangesAsync();
        return item.Id;
    }

    /// <summary>Creates a knowledge base (with one category) for articles.</summary>
    /// <param name="active">Whether the knowledge base is active.</param>
    /// <returns>The knowledge base and category identifiers.</returns>
    public async Task<(Guid BaseId, Guid CategoryId)> AddKnowledgeBaseAsync(bool active = true)
    {
        var knowledgeBase = new KnowledgeBase { Name = "KB " + Guid.NewGuid().ToString("N")[..8], IsActive = active };
        var category = new KnowledgeCategory { KnowledgeBaseId = knowledgeBase.Id, Name = "Access", IsActive = true };
        await using var db = Create();
        db.Set<KnowledgeBase>().Add(knowledgeBase);
        db.Set<KnowledgeCategory>().Add(category);
        await db.SaveChangesAsync();
        return (knowledgeBase.Id, category.Id);
    }

    /// <summary>Creates a knowledge article.</summary>
    /// <param name="knowledgeBase">The knowledge base and category.</param>
    /// <param name="title">The title.</param>
    /// <param name="state">The workflow state.</param>
    /// <param name="validTo">An optional expiry date.</param>
    /// <returns>The article identifier.</returns>
    public async Task<Guid> AddArticleAsync((Guid BaseId, Guid CategoryId) knowledgeBase, string title, KnowledgeArticleState state, DateOnly? validTo = null)
    {
        var article = new KnowledgeArticle
        {
            Number = "KB" + Random.Shared.Next(100000, 999999),
            Title = title,
            Summary = "How to reset your VPN password",
            Body = "## Steps\n\nOpen the VPN client and reset the password.\n\n## More\n\nCall the service desk.",
            KnowledgeBaseId = knowledgeBase.BaseId,
            CategoryId = knowledgeBase.CategoryId,
            State = state,
            PublishedAt = state == KnowledgeArticleState.Published ? Clock.UtcNow.AddDays(-1) : null,
            ValidTo = validTo,
        };
        await using var db = Create();
        db.Set<KnowledgeArticle>().Add(article);
        await db.SaveChangesAsync();
        return article.Id;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
        Paths.Dispose();
    }
}
