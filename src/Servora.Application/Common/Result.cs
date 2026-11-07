namespace Servora.Application.Common;

/// <summary>
/// Outcome of an application operation that does not return a value. Use results for expected failures
/// (validation, not found, forbidden) and exceptions only for unexpected technical errors.
/// </summary>
public class Result
{
    /// <summary>Initializes a new instance of the <see cref="Result"/> class.</summary>
    /// <param name="succeeded">Whether the operation succeeded.</param>
    /// <param name="error">The error message for failures.</param>
    /// <param name="errorKind">The error classification for failures.</param>
    protected Result(bool succeeded, string? error, ResultErrorKind errorKind)
    {
        Succeeded = succeeded;
        Error = error;
        ErrorKind = errorKind;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the user-safe error message when the operation failed.</summary>
    public string? Error { get; }

    /// <summary>Gets the error classification.</summary>
    public ResultErrorKind ErrorKind { get; }

    /// <summary>Creates a successful result.</summary>
    /// <returns>The result.</returns>
    public static Result Success() => new(true, null, ResultErrorKind.None);

    /// <summary>Creates a validation failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static Result Invalid(string error) => new(false, error, ResultErrorKind.Validation);

    /// <summary>Creates a not-found failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static Result NotFound(string error = "The record was not found.") => new(false, error, ResultErrorKind.NotFound);

    /// <summary>Creates an authorization failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static Result Forbidden(string error = "You do not have permission to perform this action.") => new(false, error, ResultErrorKind.Forbidden);

    /// <summary>Creates a conflict failure (concurrency, duplicates).</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static Result Conflict(string error) => new(false, error, ResultErrorKind.Conflict);

    /// <summary>Creates a generic failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static Result Failure(string error) => new(false, error, ResultErrorKind.Failure);
}
