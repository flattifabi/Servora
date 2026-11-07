using Servora.Application.Common;

namespace Servora.Web.Api;

/// <summary>
/// REST conventions shared by all <c>/api/v1</c> endpoints: <see cref="Result"/> → HTTP status + RFC 7807 problem
/// details, bounded paging (<see cref="MaxPageSize"/>) and the standard paged envelope.
/// </summary>
public static class ApiResults
{
    /// <summary>The maximum page size of REST list endpoints.</summary>
    public const int MaxPageSize = 100;

    /// <summary>The default page size of REST list endpoints.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Maps a failed result to a problem response.</summary>
    /// <param name="result">The failed result.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult Problem(Result result) => result.ErrorKind switch
    {
        ResultErrorKind.Validation => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: result.Error),
        ResultErrorKind.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: result.Error),
        ResultErrorKind.Forbidden => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: result.Error),
        ResultErrorKind.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.Error),
        _ => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Request could not be processed", detail: result.Error),
    };

    /// <summary>Returns 200 with the value, or the problem response of a failure.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult From<T>(Result<T> result) => result.Succeeded ? Results.Ok(result.Value) : Problem(result);

    /// <summary>Returns 204, or the problem response of a failure.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult From(Result result) => result.Succeeded ? Results.NoContent() : Problem(result);

    /// <summary>Returns the paged envelope, or the problem response of a failure.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult Paged<T>(Result<PagedResult<T>> result) => result.Succeeded
        ? Results.Ok(new ApiPagedResponse<T>(result.Value!.Items, result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.PageCount))
        : Problem(result);

    /// <summary>Builds a bounded page request from query parameters.</summary>
    /// <param name="page">The 1-based page.</param>
    /// <param name="pageSize">The page size (clamped to 1..<see cref="MaxPageSize"/>).</param>
    /// <param name="search">The search term (max 200 characters).</param>
    /// <param name="sort">The sort field; prefix with <c>-</c> for descending.</param>
    /// <returns>The page request.</returns>
    public static PageRequest Page(int? page, int? pageSize, string? search = null, string? sort = null) => new()
    {
        Page = page ?? 1,
        PageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize),
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Length > 200 ? search[..200] : search,
        SortBy = string.IsNullOrWhiteSpace(sort) ? null : sort.TrimStart('-'),
        SortDescending = sort?.StartsWith('-') == true,
    };
}
