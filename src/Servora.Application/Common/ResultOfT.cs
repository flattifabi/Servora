namespace Servora.Application.Common;

/// <summary>Outcome of an application operation that returns a value on success.</summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Result<T> : Result
{
    private Result(bool succeeded, T? value, string? error, ResultErrorKind errorKind)
        : base(succeeded, error, errorKind)
    {
        Value = value;
    }

    /// <summary>Gets the value. Only meaningful when <see cref="Result.Succeeded"/> is <see langword="true"/>.</summary>
    public T? Value { get; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static Result<T> Success(T value) => new(true, value, null, ResultErrorKind.None);

    /// <summary>Creates a failed result with the same error as another result.</summary>
    /// <param name="other">The failed result to copy.</param>
    /// <returns>The result.</returns>
    public static Result<T> From(Result other) => new(false, default, other.Error, other.ErrorKind);

    /// <summary>Creates a validation failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static new Result<T> Invalid(string error) => new(false, default, error, ResultErrorKind.Validation);

    /// <summary>Creates a not-found failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static new Result<T> NotFound(string error = "The record was not found.") => new(false, default, error, ResultErrorKind.NotFound);

    /// <summary>Creates an authorization failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static new Result<T> Forbidden(string error = "You do not have permission to perform this action.") => new(false, default, error, ResultErrorKind.Forbidden);

    /// <summary>Creates a conflict failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static new Result<T> Conflict(string error) => new(false, default, error, ResultErrorKind.Conflict);

    /// <summary>Creates a generic failure.</summary>
    /// <param name="error">The user-safe message.</param>
    /// <returns>The result.</returns>
    public static new Result<T> Failure(string error) => new(false, default, error, ResultErrorKind.Failure);
}
