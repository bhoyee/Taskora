namespace TodoApp.Application.Common;

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

    public bool IsSuccess { get; }

    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException(
                "A failed result does not contain a value.");

    public ApplicationError Error { get; }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(ApplicationError error) => new(error);
}
