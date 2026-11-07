using Servora.Application.Common;
using Servora.Application.Integration.DataSources;
using Servora.Domain.Integration;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary><see cref="IDataSourceInvoker"/> returning a configured response body without network access.</summary>
internal sealed class StubDataSourceInvoker : IDataSourceInvoker
{
    /// <summary>Gets or sets the response body.</summary>
    public string Body { get; set; } = "[]";

    /// <summary>Gets or sets the HTTP status code.</summary>
    public int StatusCode { get; set; } = 200;

    /// <summary>Gets the number of calls.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public Task<Result<DataSourceInvocationResult>> InvokeAsync(DataSource source, DataSourceEndpoint endpoint, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Result<DataSourceInvocationResult>.Success(new DataSourceInvocationResult
        {
            StatusCode = StatusCode,
            ContentType = "application/json",
            Body = Body,
            ResponseBytes = Body.Length,
            Method = endpoint.Method,
            Host = "api.example.test",
        }));
    }
}
