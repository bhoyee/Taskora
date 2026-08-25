namespace TodoApp.Application.Common;

/// <summary>
/// Generic success/failure wrapper used throughout the application layer
/// instead of throwing exceptions for expected failure conditions (validation,
/// not-found, conflict, forbidden, unauthorized). A <see cref="Result{T}"/> is
/// either a success carrying a <typeparamref name="T"/> <see cref="Value"/>,
/// or a failure carrying a typed <see cref="ApplicationError"/>. Handlers
/// construct instances via the <see cref="Success"/> and <see cref="Failure"/>
/// factory methods rather than a public constructor.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        Error = ApplicationError.None;
    }

    private Result(ApplicationError error)
    {
        IsSuccess = false;
        Error = error;
    }

    /// <summary>Indicates whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The success value. Throws <see cref="InvalidOperationException"/> if
    /// accessed on a failed result; callers should check <see cref="IsSuccess"/>
    /// (or <see cref="Error"/>) before reading this property.
    /// </summary>
    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException(
                "A failed result does not contain a value.");

    /// <summary>
    /// The error describing why the operation failed. Equal to
    /// <see cref="ApplicationError.None"/> for successful results.
    /// </summary>
    public ApplicationError Error { get; }

    /// <summary>Creates a successful result wrapping the given value.</summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Creates a failed result carrying the given typed error.</summary>
    public static Result<T> Failure(ApplicationError error) => new(error);
}
