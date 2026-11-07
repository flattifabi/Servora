using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;

namespace Servora.Application.Ai;

/// <summary>A dedicated DI scope in which a sub-agent runs under its own <see cref="ActorInfo"/>.</summary>
/// <param name="scope">The DI scope.</param>
/// <param name="actor">The agent identity.</param>
public sealed class AiAgentScope(AsyncServiceScope scope, ActorInfo actor) : IAsyncDisposable
{
    /// <summary>Gets the scoped services (resolve tools from here so they see the agent identity).</summary>
    public IServiceProvider Services => scope.ServiceProvider;

    /// <summary>Gets the agent identity.</summary>
    public ActorInfo Actor { get; } = actor;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
