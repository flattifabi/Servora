using System.Text.Json.Nodes;
using Servora.Application.Automation.Actions;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary>Test step type <c>test.flaky</c> that fails (retryable) a configurable number of times before succeeding.</summary>
internal sealed class FlakyAction : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "test.flaky";

    /// <summary>Gets or sets the number of failures before the first success.</summary>
    public int FailuresBeforeSuccess { get; set; }

    /// <summary>Gets the number of executions.</summary>
    public int Executions { get; private set; }

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Flaky", "Test", "zap", "danger", "Fails a few times, then succeeds.")
    {
        Outputs = [new FlowActionOutput("attempt", "Attempt")],
    };

    /// <inheritdoc />
    public Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        Executions++;
        return Task.FromResult(Executions <= FailuresBeforeSuccess
            ? FlowActionResult.Fail($"Transient failure {Executions}", retryable: true)
            : FlowActionResult.Success(new JsonObject { ["attempt"] = Executions }));
    }
}
