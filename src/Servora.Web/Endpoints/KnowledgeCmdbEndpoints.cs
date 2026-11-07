using Servora.Application.Cmdb;
using Servora.Application.Cmdb.Graph;
using Servora.Application.Common;
using Servora.Application.Knowledge;

namespace Servora.Web.Endpoints;

/// <summary>HTTP endpoints of the T-06 Knowledge, CMDB &amp; Assets module.</summary>
/// <remarks>
/// Owned by the module's task. Only the owning task edits this file. The endpoints are read-only adapters over the
/// application services, which perform the permission checks (<c>knowledge.read</c>, <c>cmdb.read</c>); the route group
/// additionally requires an authenticated caller.
/// </remarks>
public static class KnowledgeCmdbEndpoints
{
    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapKnowledgeCmdbEndpoints(this IEndpointRouteBuilder app)
    {
        var knowledge = app.MapGroup("/api/knowledge").RequireAuthorization().WithTags("Knowledge");
        knowledge.MapGet("/search", SearchKnowledgeAsync).WithName("SearchKnowledge")
            .WithSummary("Ranked search over published knowledge articles.");
        knowledge.MapGet("/articles/{id:guid}", GetArticleAsync).WithName("GetKnowledgeArticle")
            .WithSummary("Gets a knowledge article with its body rendered to safe HTML.");

        var cmdb = app.MapGroup("/api/cmdb").RequireAuthorization().WithTags("CMDB");
        cmdb.MapGet("/cis/{id:guid}/graph", GetGraphAsync).WithName("GetCiDependencyGraph")
            .WithSummary("Gets the upstream / downstream dependency graph of a configuration item.");
        return app;
    }

    private static async Task<IResult> SearchKnowledgeAsync(string? q, int? take, IKnowledgeSearchService search, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.Ok(Array.Empty<KnowledgeSearchHit>());
        }

        var query = q.Length > 200 ? q[..200] : q;
        return ToHttp(await search.SearchAsync(new KnowledgeSearchRequest(query, Math.Clamp(take ?? 10, 1, 50)), cancellationToken));
    }

    private static async Task<IResult> GetArticleAsync(Guid id, IKnowledgeService knowledge, CancellationToken cancellationToken) =>
        ToHttp(await knowledge.GetAsync(id, cancellationToken));

    private static async Task<IResult> GetGraphAsync(Guid id, int? depth, CiGraphDirection? direction, ICmdbService cmdb, CancellationToken cancellationToken) =>
        ToHttp(await cmdb.GetGraphAsync(id, Math.Clamp(depth ?? 2, 1, 4), direction ?? CiGraphDirection.Both, cancellationToken));

    private static IResult ToHttp<T>(Result<T> result) => result.Succeeded
        ? Results.Ok(result.Value)
        : result.ErrorKind switch
        {
            ResultErrorKind.Forbidden => Results.Problem(result.Error, statusCode: StatusCodes.Status403Forbidden),
            ResultErrorKind.NotFound => Results.Problem(result.Error, statusCode: StatusCodes.Status404NotFound),
            ResultErrorKind.Validation => Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest),
            _ => Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError),
        };
}
