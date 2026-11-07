namespace Servora.Application.Common;

/// <summary>Classification of a failed <see cref="Result"/>, mapped to HTTP status codes by the API layer.</summary>
public enum ResultErrorKind
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>Invalid input (HTTP 400).</summary>
    Validation = 1,

    /// <summary>Resource not found (HTTP 404).</summary>
    NotFound = 2,

    /// <summary>Not permitted (HTTP 403).</summary>
    Forbidden = 3,

    /// <summary>Conflict, for example concurrency or duplicate (HTTP 409).</summary>
    Conflict = 4,

    /// <summary>Other expected failure (HTTP 422).</summary>
    Failure = 5,
}
