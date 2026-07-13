using TodoApp.Application.Common;

namespace TodoApp.Api.Endpoints;

internal static class ApiResult
{
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

    private static string TitleFor(ErrorType type) =>
        type switch
        {
            ErrorType.Validation => "Request validation failed",
            ErrorType.NotFound => "Resource not found",
            ErrorType.Conflict => "Business rule conflict",
            _ => "Unexpected application error"
        };
}
