namespace TodoApp.Application.Accounts;

/// <summary>Command to register a new user account and its initial workspace.</summary>
public sealed record RegisterAccountCommand(
    string DisplayName,
    string Email,
    string Password,
    string WorkspaceName);

/// <summary>Command to authenticate a user with email and password.</summary>
public sealed record LoginCommand(string Email, string Password);

/// <summary>Query to fetch the profile of the currently authenticated user.</summary>
public sealed record GetCurrentAccountQuery;

/// <summary>Command to update the current user's account email.</summary>
public sealed record UpdateAccountProfileCommand(string Email);

/// <summary>Command to change the current user's password, verifying the current one.</summary>
public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword);

/// <summary>Command to request a password reset code be emailed to the given address.</summary>
public sealed record RequestPasswordResetCommand(string Email);

/// <summary>Command to reset a password using a previously issued reset token/code.</summary>
public sealed record ResetPasswordWithTokenCommand(
    string Email,
    string Token,
    string NewPassword);

/// <summary>Represents an authenticated session for a user, including their access token.</summary>
public sealed record AccountSessionDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string AccessToken);

/// <summary>Represents a user's basic account profile information.</summary>
public sealed record AccountProfileDto(
    Guid UserId,
    string DisplayName,
    string Email);
