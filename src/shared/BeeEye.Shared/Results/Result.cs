namespace BeeEye.Shared.Results;

/// <summary>A structured, machine-readable failure. Maps cleanly onto Problem Details.</summary>
/// <param name="Code">Stable, kebab-or-snake machine code (e.g. "not_found").</param>
/// <param name="Message">Human-readable, non-sensitive description.</param>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string message) => new("validation", message);
    public static Error NotFound(string message) => new("not_found", message);
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>
    /// The request is well-formed and understood, but its content cannot be acted on — a value
    /// outside a mandated bound, or a client working from a stale copy of the record. Distinct from
    /// <see cref="Validation"/>, which means the request itself is malformed.
    /// </summary>
    public static Error Unprocessable(string message) => new("unprocessable", message);
    public static Error Forbidden(string message) => new("forbidden", message);
    public static Error Unavailable(string message) => new("unavailable", message);
}

/// <summary>
/// Explicit success/failure result — the platform models expected failure with
/// values rather than exceptions, so callers must handle both paths.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

/// <summary>A <see cref="Result"/> that carries a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, Error error) : base(isSuccess, error)
        => _value = value;

    /// <summary>The value. Throws if accessed on a failed result — check <see cref="Result.IsSuccess"/> first.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static Result<T> Success(T value) => new(true, value, Error.None);
    public static new Result<T> Failure(Error error) => new(false, default, error);

    /// <summary>Project the value on success; propagate the error otherwise.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map)
        => IsSuccess ? Result<TOut>.Success(map(Value)) : Result<TOut>.Failure(Error);
}
