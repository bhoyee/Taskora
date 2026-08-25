namespace TodoApp.Application.Common;

/// <summary>
/// Represents a typed application-layer error carried inside a failed
/// <see cref="Result{T}"/>. Encapsulates a machine-readable <see cref="Code"/>,
/// a human-readable <see cref="Description"/>, and an <see cref="ErrorType"/>
/// classification that presentation layers (e.g. API controllers) use to map
/// the failure onto an appropriate HTTP status code or UI treatment.
/// </summary>
public sealed record ApplicationError(
    string Code,
    string Description,
    ErrorType Type)
{
    /// <summary>
    /// The sentinel "no error" value used by successful results, where no
    /// error information is applicable.
    /// </summary>
    public static readonly ApplicationError None =
        new(string.Empty, string.Empty, ErrorType.None);
}
