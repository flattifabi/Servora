namespace Servora.Application.Automation.Services;

/// <summary>Names of secret store entries owned by flows.</summary>
public static class FlowSecretNames
{
    /// <summary>Gets the secret name of a flow's inbound webhook secret.</summary>
    /// <param name="flowId">The flow.</param>
    /// <returns>The secret name.</returns>
    public static string Webhook(Guid flowId) => $"flow.{flowId:N}.webhook_secret";
}
