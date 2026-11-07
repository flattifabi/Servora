using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>In-memory <see cref="IServiceManager"/> that records calls and can simulate failures.</summary>
internal sealed class FakeServiceManager : IServiceManager
{
    /// <summary>Gets or sets the registered service (<see langword="null"/> = not installed).</summary>
    public ServiceInfo? Service { get; set; }

    /// <summary>Gets the service environment variables.</summary>
    public Dictionary<string, string> Environment { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the recorded calls (create, delete, start, stop).</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Gets the last created definition.</summary>
    public ServiceDefinition? Created { get; private set; }

    /// <summary>Gets or sets the number of upcoming <see cref="Start"/> calls that fail.</summary>
    public int StartFailures { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="Stop"/> fails.</summary>
    public bool FailStop { get; set; }

    /// <summary>Registers an installed service.</summary>
    /// <param name="executablePath">The executable path.</param>
    /// <param name="state">The state.</param>
    public void Install(string executablePath, ServiceState state = ServiceState.Running) =>
        Service = new ServiceInfo("Servora", "Servora", state, "Automatic", true, $"\"{executablePath}\"", @"NT SERVICE\Servora");

    /// <inheritdoc />
    public ServiceInfo? Query(string serviceName) => Service;

    /// <inheritdoc />
    public ServiceState GetState(string serviceName) => Service?.State ?? ServiceState.NotInstalled;

    /// <inheritdoc />
    public void Create(ServiceDefinition definition)
    {
        Calls.Add("create");
        Created = definition;
        foreach (var pair in definition.Environment)
        {
            Environment[pair.Key] = pair.Value;
        }

        Install(definition.ExecutablePath, ServiceState.Stopped);
    }

    /// <inheritdoc />
    public void Delete(string serviceName)
    {
        Calls.Add("delete");
        Service = null;
        Environment.Clear();
    }

    /// <inheritdoc />
    public void Start(string serviceName, TimeSpan timeout)
    {
        Calls.Add("start");
        if (Service is null)
        {
            throw new InstallerException("not installed");
        }

        if (StartFailures > 0)
        {
            StartFailures--;
            Service = Service with { State = ServiceState.Stopped };
            throw new InstallerException("Simulated start failure");
        }

        Service = Service with { State = ServiceState.Running };
    }

    /// <inheritdoc />
    public void Stop(string serviceName, TimeSpan timeout)
    {
        Calls.Add("stop");
        if (FailStop)
        {
            throw new InstallerException("Simulated stop timeout");
        }

        if (Service is not null)
        {
            Service = Service with { State = ServiceState.Stopped };
        }
    }

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string serviceName, string variable) =>
        Environment.TryGetValue(variable, out var value) ? value : null;
}
