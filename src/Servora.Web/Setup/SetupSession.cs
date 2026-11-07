using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Infrastructure.Configuration;

namespace Servora.Web.Setup;

/// <summary>
/// Circuit-scoped gatekeeper of the setup wizard. Every wizard operation goes through this class, which enforces that
/// the client is local (loopback, no proxy headers) or has entered the setup token, and that setup is still open.
/// It never touches the database itself, so the wizard works while no database is configured.
/// </summary>
/// <param name="setup">The setup backend.</param>
/// <param name="httpContextAccessor">The HTTP context accessor (fallback when the circuit handler did not run).</param>
/// <param name="logger">The logger.</param>
public sealed class SetupSession(ISetupService setup, IHttpContextAccessor httpContextAccessor, ILogger<SetupSession> logger)
{
    /// <summary>The number of wrong tokens after which the session refuses further attempts.</summary>
    public const int MaxTokenAttempts = 10;

    private bool _clientEvaluated;
    private bool _isLocalClient;
    private int _failedAttempts;

    /// <summary>Gets a value indicating whether the client connects from the local machine.</summary>
    public bool IsLocalClient
    {
        get
        {
            if (!_clientEvaluated && httpContextAccessor.HttpContext is { } context)
            {
                CaptureClient(context);
            }

            return _isLocalClient;
        }
    }

    /// <summary>Gets a value indicating whether the setup token was entered successfully in this session.</summary>
    public bool TokenVerified { get; private set; }

    /// <summary>Gets a value indicating whether the client may use the wizard.</summary>
    public bool IsAuthorized => IsLocalClient || TokenVerified;

    /// <summary>Gets a value indicating whether too many wrong tokens were entered.</summary>
    public bool IsLockedOut => _failedAttempts >= MaxTokenAttempts;

    /// <summary>Gets a value indicating whether this session completed the installation.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Gets a value indicating whether setup can (still) be run in this process.</summary>
    public bool CanRunSetup => !IsCompleted && (!setup.IsConfigured || SetupState.IsInProgress || setup.HasIncompleteInstallation);

    /// <summary>Gets the setup backend for read-only information (providers, data directory, token path).</summary>
    public ISetupService Backend => setup;

    /// <summary>Evaluates whether the connection of the given HTTP context is local. Called once per circuit.</summary>
    /// <param name="context">The HTTP context of the circuit connection.</param>
    public void CaptureClient(HttpContext context)
    {
        var forwarded = SetupAccessPolicy.ForwardingHeaders.Any(h => context.Request.Headers.ContainsKey(h));
        _isLocalClient = SetupAccessPolicy.IsLocalClient(context.Connection.RemoteIpAddress, context.Connection.LocalIpAddress, forwarded);
        _clientEvaluated = true;
    }

    /// <summary>Verifies the setup token. Wrong attempts are delayed and limited per session.</summary>
    /// <param name="token">The token entered by the user.</param>
    /// <returns>The result.</returns>
    public async Task<Result> VerifyTokenAsync(string? token)
    {
        if (IsLockedOut)
        {
            return Result.Forbidden("Too many wrong attempts. Reload the page to try again.");
        }

        if (setup.ValidateSetupToken(token))
        {
            TokenVerified = true;
            logger.LogInformation("Setup wizard unlocked with the setup token by a remote client.");
            return Result.Success();
        }

        _failedAttempts++;
        logger.LogWarning("Wrong setup token entered (attempt {Attempt}).", _failedAttempts);
        await Task.Delay(TimeSpan.FromSeconds(1));
        return Result.Invalid("The setup token is not correct.");
    }

    /// <summary>Composes the connection string from the wizard fields.</summary>
    /// <param name="input">The fields.</param>
    /// <returns>The connection string or an error.</returns>
    public Result<string> ComposeConnectionString(SetupConnectionInput input) =>
        Guard() is { } denied ? Result<string>.From(denied) : setup.ComposeConnectionString(input);

    /// <summary>Tests a connection.</summary>
    /// <param name="provider">The provider key.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The round-trip time or an error.</returns>
    public Task<Result<TimeSpan>> TestConnectionAsync(string provider, string connectionString, CancellationToken cancellationToken = default) =>
        Guard() is { } denied ? Task.FromResult(Result<TimeSpan>.From(denied)) : setup.TestConnectionAsync(provider, connectionString, cancellationToken);

    /// <summary>Runs the installation while keeping the setup route reachable (<see cref="SetupState.IsInProgress"/>).</summary>
    /// <param name="provider">The provider key.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="organization">The organisation settings.</param>
    /// <param name="progress">Receives progress updates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<Result> InstallAsync(string provider, string connectionString, SetupOrganizationInput organization, IProgress<SetupProgressUpdate> progress, CancellationToken cancellationToken = default)
    {
        if (Guard() is { } denied)
        {
            return denied;
        }

        SetupState.IsInProgress = true;
        var result = Result.Failure("The installation did not complete.");
        try
        {
            result = await setup.InstallAsync(provider, connectionString, organization, progress, cancellationToken);
            IsCompleted = result.Succeeded;
            return result;
        }
        finally
        {
            // Keep the wizard reachable only while a saved-but-unfinished installation can be retried.
            SetupState.IsInProgress = !result.Succeeded && setup.HasIncompleteInstallation;
        }
    }

    /// <summary>Gets the redacted form of a connection string for display.</summary>
    /// <param name="provider">The provider key.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The redacted connection string.</returns>
    public string Redact(string provider, string connectionString) => setup.Redact(provider, connectionString);

    private Result? Guard()
    {
        if (!IsAuthorized)
        {
            return Result.Forbidden("Enter the setup token first.");
        }

        return CanRunSetup ? null : Result.Conflict("Setup has already been completed.");
    }
}
