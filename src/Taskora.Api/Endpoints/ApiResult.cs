using TodoApp.Application.Common;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Translates application-layer <see cref="Result{T}"/> values into minimal-API
/// <see cref="IResult"/> responses, so every endpoint maps errors to HTTP
/// status codes/ProblemDetails consistently.
/// </summary>
internal static class ApiResult
{
    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an HTTP response: 200 OK with
    /// the value on success, or a ProblemDetails response (with a status code
    /// derived from <see cref="ErrorType"/> and the error code in the
    /// "code" extension) on failure.
    /// </summary>
    public static IResult From<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var statusCode = result.Error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            statusCode: statusCode,
            title: TitleFor(result.Error.Type),
            detail: result.Error.Description,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.Error.Code
            });
    }

    // Maps an ErrorType to a human-readable ProblemDetails title.
    private static string TitleFor(ErrorType type) =>
        type switch
        {
            ErrorType.Validation => "Request validation failed",
            ErrorType.NotFound => "Resource not found",
            ErrorType.Conflict => "Business rule conflict",
            ErrorType.Forbidden => "Access denied",
            ErrorType.Unauthorized => "Authentication required",
            _ => "Unexpected application error"
        };
}
