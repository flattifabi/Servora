using Servora.Installer.Model;

namespace Servora.Installer.Abstractions;

/// <summary>Controls Windows services (Service Control Manager). Faked in unit tests.</summary>
internal interface IServiceManager
{
    /// <summary>Reads the configuration and state of a service.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <returns>The service information, or <see langword="null"/> when the service is not installed.</returns>
    ServiceInfo? Query(string serviceName);

    /// <summary>Gets the current runtime state of a service.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <returns>The state (<see cref="ServiceState.NotInstalled"/> when missing).</returns>
    ServiceState GetState(string serviceName);

    /// <summary>
    /// Registers a service with delayed automatic start, restart-on-failure recovery actions, an unrestricted service
    /// SID and the given environment variables.
    /// </summary>
    /// <param name="definition">The service definition.</param>
    /// <exception cref="InstallerException">Registration failed.</exception>
    void Create(ServiceDefinition definition);

    /// <summary>Removes a service registration.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <exception cref="InstallerException">Removal failed.</exception>
    void Delete(string serviceName);

    /// <summary>Starts a service and waits until it reports <see cref="ServiceState.Running"/>.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="timeout">The maximum wait time.</param>
    /// <exception cref="InstallerException">The service did not start in time.</exception>
    void Start(string serviceName, TimeSpan timeout);

    /// <summary>Stops a service (when running) and waits until it reports <see cref="ServiceState.Stopped"/>.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="timeout">The maximum wait time.</param>
    /// <exception cref="InstallerException">The service did not stop in time.</exception>
    void Stop(string serviceName, TimeSpan timeout);

    /// <summary>Reads a service-specific environment variable (registry value <c>Environment</c> of the service).</summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="variable">The variable name.</param>
    /// <returns>The value, or <see langword="null"/> when not set.</returns>
    string? GetEnvironmentVariable(string serviceName, string variable);
}
