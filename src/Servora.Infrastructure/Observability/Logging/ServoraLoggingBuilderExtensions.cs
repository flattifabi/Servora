using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>Registers the Servora logger provider, its sinks and the runtime debug switch.</summary>
public static class ServoraLoggingBuilderExtensions
{
    /// <summary>
    /// Adds the Servora logger provider (ring buffer + rolling JSON-lines files with redaction) and the runtime log level
    /// switch (<see cref="ILogLevelController"/>) that changes filter levels without restart.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="logDirectory">Resolves the log directory (normally <c>IAppPaths.LogDirectory</c>).</param>
    /// <param name="configuration">The configuration (section <c>Servora:Logging</c>, <c>Logging:LogLevel:Default</c>).</param>
    /// <returns>The builder.</returns>
    public static ILoggingBuilder AddServoraLogging(this ILoggingBuilder builder, Func<IServiceProvider, string> logDirectory, IConfiguration? configuration = null)
    {
        var options = configuration?.GetSection(ServoraLoggingOptions.SectionName).Get<ServoraLoggingOptions>() ?? new ServoraLoggingOptions();
        var services = builder.Services;

        services.TryAddSingleton(new InMemoryLogSink(options.RingBufferCapacity));
        services.TryAddSingleton<ILogStore>(sp => sp.GetRequiredService<InMemoryLogSink>());
        services.TryAddSingleton(sp => new JsonFileLogSink(
            logDirectory(sp),
            new LogRetentionSettings(Math.Clamp(options.RetentionDays, 1, 365), Math.Clamp(options.MaxFileSizeMb, 1, 1024)),
            options.FileQueueCapacity));
        services.TryAddSingleton<ILogFileStore>(sp => sp.GetRequiredService<JsonFileLogSink>());

        services.TryAddSingleton(sp => new LogLevelSwitch(configuration ?? sp.GetService<IConfiguration>()));
        services.TryAddSingleton<ILogLevelController>(sp => sp.GetRequiredService<LogLevelSwitch>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<LoggerFilterOptions>, LogLevelFilterPostConfigure>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOptionsChangeTokenSource<LoggerFilterOptions>, LogLevelChangeTokenSource>());

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ServoraLoggerProvider>());
        return builder;
    }
}
