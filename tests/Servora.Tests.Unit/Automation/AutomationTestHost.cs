using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Activity;
using Servora.Application.Approvals;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Integration.DataSources;
using Servora.Application.Notifications;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Automation;
using Servora.Infrastructure.Integration;
using Servora.Infrastructure.Modules;
using Servora.Infrastructure.Persistence;
using Servora.Tests.Unit.Automation.Fakes;

namespace Servora.Tests.Unit.Automation;

/// <summary>
/// In-memory SQLite host with the real Automation &amp; Integration module registrations and fakes for the kernel
/// contracts (records, e-mail, approvals, secrets, tables, conditions, DNS). Background workers are not started; tests
/// drive the engine directly through <see cref="RunAsync"/>.
/// </summary>
internal sealed class AutomationTestHost : IAppDbContextFactory, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ServoraDbContext> _options;

    private AutomationTestHost()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(_connection).Options;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Servora:Automation:ApprovalPollSeconds"] = "5" })
            .Build());
        services.AddSingleton<IAppDbContextFactory>(this);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<ICurrentUser>(User);
        services.AddScoped<ActorContext>();
        services.AddSingleton<ISecretStore>(Secrets);
        services.AddSingleton<IAuditService>(Audit);
        services.AddSingleton<ITableRegistry>(new FakeTableRegistry());
        services.AddSingleton<IRecordService>(Records);
        services.AddSingleton<IConditionEvaluator>(new SimpleConditionEvaluator());
        services.AddSingleton<IEmailSender>(Email);
        services.AddSingleton<IApprovalService>(Approvals);
        services.AddScoped<IActivityService, ActivityService>();

        services.AddAutomationModule();

        // Test overrides (last registration wins).
        services.AddSingleton<IDnsResolver>(Dns);
        services.AddSingleton<INetworkAllowListProvider>(AllowList);
        services.AddSingleton<IDataSourceInvoker>(Invoker);
        services.AddSingleton<IFlowAction>(Flaky);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>Gets the service provider.</summary>
    public ServiceProvider Services { get; }

    /// <summary>Gets the clock.</summary>
    public TestClock Clock { get; } = new();

    /// <summary>Gets the current user.</summary>
    public TestCurrentUser User { get; } = new();

    /// <summary>Gets the record service fake.</summary>
    public InMemoryRecordService Records { get; } = new();

    /// <summary>Gets the e-mail fake.</summary>
    public RecordingEmailSender Email { get; } = new();

    /// <summary>Gets the approval fake.</summary>
    public FakeApprovalService Approvals { get; } = new();

    /// <summary>Gets the secret store fake.</summary>
    public InMemorySecretStore Secrets { get; } = new();

    /// <summary>Gets the audit fake.</summary>
    public RecordingAuditService Audit { get; } = new();

    /// <summary>Gets the DNS fake.</summary>
    public FakeDnsResolver Dns { get; } = new();

    /// <summary>Gets the network allow-list fake.</summary>
    public StaticAllowListProvider AllowList { get; } = new();

    /// <summary>Gets the HTTP invoker stub.</summary>
    public StubDataSourceInvoker Invoker { get; } = new();

    /// <summary>Gets the flaky test action.</summary>
    public FlakyAction Flaky { get; } = new();

    /// <summary>Creates a host with the schema.</summary>
    /// <returns>The host.</returns>
    public static async Task<AutomationTestHost> CreateAsync()
    {
        var host = new AutomationTestHost();
        await using var db = host.Create();
        await db.Database.EnsureCreatedAsync();
        return host;
    }

    /// <summary>Creates a context.</summary>
    /// <returns>The context.</returns>
    public ServoraDbContext Create() => new(_options) { ActorName = User.DisplayName, ActorUserId = User.UserId };

    /// <inheritdoc />
    public IAppDbContext CreateDbContext() => Create();

    /// <inheritdoc />
    public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAppDbContext>(Create());

    /// <summary>Resolves a service in a new scope and runs an action with it.</summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>The result.</returns>
    public async Task<TResult> WithAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    /// <summary>Persists a flow with the given steps.</summary>
    /// <param name="steps">The steps.</param>
    /// <param name="configure">Optional changes to the flow before saving.</param>
    /// <returns>The flow.</returns>
    public async Task<Flow> AddFlowAsync(IEnumerable<FlowStepDefinition> steps, Action<Flow>? configure = null)
    {
        var flow = new Flow { Name = $"Flow {Guid.NewGuid():N}"[..13], IsActive = true, StepsJson = FlowJson.Serialize(steps.ToList()) };
        configure?.Invoke(flow);
        await using var db = Create();
        db.Set<Flow>().Add(flow);
        await db.SaveChangesAsync();
        return flow;
    }

    /// <summary>Starts a manual run of a flow.</summary>
    /// <param name="flowId">The flow.</param>
    /// <param name="idempotencyKey">An optional idempotency key.</param>
    /// <returns>The run identifier.</returns>
    public async Task<Guid> StartAsync(Guid flowId, string? idempotencyKey = null)
    {
        var result = await WithAsync<IFlowRunStarter, Result<Guid>>(s => s.StartAsync(new FlowStartRequest { FlowId = flowId, TriggerType = FlowTriggerType.Manual, IdempotencyKey = idempotencyKey }));
        Assert.True(result.Succeeded, result.Error);
        return result.Value;
    }

    /// <summary>Executes (or resumes) a run segment with the real flow runner.</summary>
    /// <param name="runId">The run.</param>
    /// <returns>The status after the segment.</returns>
    public Task<FlowRunStatus> RunAsync(Guid runId) => WithAsync<FlowRunner, FlowRunStatus>(r => r.ExecuteAsync(runId, CancellationToken.None));

    /// <summary>Loads a run.</summary>
    /// <param name="runId">The run.</param>
    /// <returns>The run.</returns>
    public async Task<FlowRun> GetRunAsync(Guid runId)
    {
        await using var db = Create();
        return await db.Set<FlowRun>().AsNoTracking().SingleAsync(r => r.Id == runId);
    }

    /// <summary>Loads the step history of a run in order.</summary>
    /// <param name="runId">The run.</param>
    /// <returns>The steps.</returns>
    public async Task<List<FlowRunStep>> GetStepsAsync(Guid runId)
    {
        await using var db = Create();
        return await db.Set<FlowRunStep>().AsNoTracking().Where(s => s.RunId == runId).OrderBy(s => s.Sequence).ToListAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
