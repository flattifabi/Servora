using System.Runtime.Versioning;
using System.ServiceProcess;
using Microsoft.Win32;
using Servora.Installer.Abstractions;
using Servora.Installer.Core;
using Servora.Installer.Model;

namespace Servora.Installer.Platform;

/// <summary>
/// <see cref="IServiceManager"/> backed by the Service Control Manager: <see cref="ServiceController"/> for state and
/// start/stop, <c>sc.exe</c> for registration and recovery settings, and the service registry key for configuration
/// that <see cref="ServiceController"/> does not expose (image path, account, delayed start, environment).
/// Querying works without administrative rights.
/// </summary>
/// <param name="processes">The process runner.</param>
/// <param name="environment">The environment (location of <c>sc.exe</c>).</param>
[SupportedOSPlatform("windows")]
internal sealed class WindowsServiceManager(IProcessRunner processes, IInstallerEnvironment environment) : IServiceManager
{
    private const int ErrorServiceDoesNotExist = 1060;
    private const int ErrorServiceMarkedForDelete = 1072;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private string Sc => Path.Combine(environment.SystemDirectory, "sc.exe");

    /// <inheritdoc />
    public ServiceInfo? Query(string serviceName)
    {
        using var controller = Find(serviceName);
        if (controller is null)
        {
            return null;
        }

        using var key = Registry.LocalMachine.OpenSubKey(RegistryPath(serviceName));
        var imagePath = key?.GetValue("ImagePath", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? string.Empty;
        var account = key?.GetValue("ObjectName") as string ?? "LocalSystem";
        var delayed = key?.GetValue("DelayedAutostart") is int flag && flag != 0;

        return new ServiceInfo(
            controller.ServiceName,
            controller.DisplayName,
            Map(controller.Status),
            controller.StartType.ToString(),
            delayed && controller.StartType == ServiceStartMode.Automatic,
            imagePath,
            account);
    }

    /// <inheritdoc />
    public ServiceState GetState(string serviceName)
    {
        using var controller = Find(serviceName);
        return controller is null ? ServiceState.NotInstalled : Map(controller.Status);
    }

    /// <inheritdoc />
    public void Create(ServiceDefinition definition)
    {
        RunSc(ScCommandBuilder.Create(definition), "register the service");
        try
        {
            RunSc(ScCommandBuilder.Description(definition.Name, definition.Description), "set the service description");
            RunSc(ScCommandBuilder.Failure(definition.Name), "configure the recovery actions");
            RunSc(ScCommandBuilder.FailureFlag(definition.Name), "configure the recovery actions");
            RunSc(ScCommandBuilder.SidType(definition.Name), "configure the service SID");
            if (definition.Environment.Count > 0)
            {
                using var key = Registry.LocalMachine.OpenSubKey(RegistryPath(definition.Name), writable: true)
                    ?? throw new InstallerException("The service registry key was not found after registration.");
                key.SetValue(
                    "Environment",
                    definition.Environment.Select(p => $"{p.Key}={p.Value}").ToArray(),
                    RegistryValueKind.MultiString);
            }
        }
        catch
        {
            processes.Run(Sc, ScCommandBuilder.Delete(definition.Name));
            throw;
        }
    }

    /// <inheritdoc />
    public void Delete(string serviceName)
    {
        var result = processes.Run(Sc, ScCommandBuilder.Delete(serviceName));
        if (!result.Succeeded && result.ExitCode is not ErrorServiceDoesNotExist and not ErrorServiceMarkedForDelete)
        {
            throw new InstallerException($"Could not remove the service (sc.exe exit code {result.ExitCode}): {result.Output}");
        }
    }

    /// <inheritdoc />
    public void Start(string serviceName, TimeSpan timeout)
    {
        using var controller = Find(serviceName) ?? throw new InstallerException($"The service '{serviceName}' does not exist.");
        if (controller.Status == ServiceControllerStatus.Running)
        {
            return;
        }

        try
        {
            if (controller.Status is ServiceControllerStatus.Stopped)
            {
                controller.Start();
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new InstallerException($"The service could not be started: {ex.InnerException?.Message ?? ex.Message}", ex);
        }

        var started = DateTime.UtcNow;
        while (true)
        {
            Thread.Sleep(PollInterval);
            controller.Refresh();
            var elapsed = DateTime.UtcNow - started;
            if (controller.Status == ServiceControllerStatus.Running)
            {
                return;
            }

            if (controller.Status == ServiceControllerStatus.Stopped && elapsed > TimeSpan.FromSeconds(3))
            {
                throw new InstallerException("The service stopped right after starting. Check the Windows Event Log (Application, source Servora) and the logs in the data directory.");
            }

            if (elapsed > timeout)
            {
                throw new InstallerException($"The service did not reach 'Running' within {timeout.TotalSeconds:0} seconds (current state: {controller.Status}).");
            }
        }
    }

    /// <inheritdoc />
    public void Stop(string serviceName, TimeSpan timeout)
    {
        using var controller = Find(serviceName);
        if (controller is null || controller.Status == ServiceControllerStatus.Stopped)
        {
            return;
        }

        try
        {
            if (controller.Status != ServiceControllerStatus.StopPending)
            {
                controller.Stop();
            }

            controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        }
        catch (System.ServiceProcess.TimeoutException ex)
        {
            throw new InstallerException($"The service did not stop within {timeout.TotalSeconds:0} seconds.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InstallerException($"The service could not be stopped: {ex.InnerException?.Message ?? ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string serviceName, string variable)
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryPath(serviceName));
        if (key?.GetValue("Environment") is not string[] entries)
        {
            return null;
        }

        var prefix = variable + "=";
        var entry = entries.LastOrDefault(e => e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return entry?[prefix.Length..];
    }

    private static string RegistryPath(string serviceName) => @"SYSTEM\CurrentControlSet\Services\" + serviceName;

    private static ServiceController? Find(string serviceName)
    {
        ServiceController? match = null;
        foreach (var controller in ServiceController.GetServices())
        {
            if (match is null && string.Equals(controller.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
            {
                match = controller;
            }
            else
            {
                controller.Dispose();
            }
        }

        return match;
    }

    private static ServiceState Map(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Stopped => ServiceState.Stopped,
        ServiceControllerStatus.StartPending => ServiceState.StartPending,
        ServiceControllerStatus.StopPending => ServiceState.StopPending,
        ServiceControllerStatus.Running => ServiceState.Running,
        ServiceControllerStatus.ContinuePending => ServiceState.ContinuePending,
        ServiceControllerStatus.PausePending => ServiceState.PausePending,
        ServiceControllerStatus.Paused => ServiceState.Paused,
        _ => ServiceState.Unknown,
    };

    private void RunSc(IReadOnlyList<string> arguments, string description)
    {
        var result = processes.Run(Sc, arguments);
        if (!result.Succeeded)
        {
            throw new InstallerException($"Could not {description} (sc.exe exit code {result.ExitCode}): {result.Output}");
        }
    }
}
