namespace TodoApp.Application.Common;

public sealed record ApplicationError(
    string Code,
    string Description,
    ErrorType Type)
{
    public static readonly ApplicationError None =
        new(string.Empty, string.Empty, ErrorType.None);
}
