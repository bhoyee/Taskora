namespace TodoApp.Application.Common;

/// <summary>
/// Classifies the nature of an <see cref="ApplicationError"/> so that callers
/// (such as API controllers) can translate it into the appropriate response
/// (e.g. HTTP status code) without inspecting the error message text.
/// </summary>
public enum ErrorType
{
    /// <summary>No error; used only by the success sentinel.</summary>
    None = 0,

    /// <summary>The request failed input or business-rule validation.</summary>
    Validation = 1,

    /// <summary>The requested resource does not exist.</summary>
    NotFound = 2,

    /// <summary>The request conflicts with the current state of the resource.</summary>
    Conflict = 3,

    /// <summary>The current user is authenticated but not permitted to perform the action.</summary>
    Forbidden = 4,

    /// <summary>The request requires authentication that is missing or invalid.</summary>
    Unauthorized = 5
}
