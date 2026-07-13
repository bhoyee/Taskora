namespace TodoApp.Application.Accounts;

public sealed record RegisterAccountCommand(
    string DisplayName,
    string Email,
    string Password,
    string WorkspaceName);

public sealed record LoginCommand(string Email, string Password);

public sealed record GetCurrentAccountQuery;

public sealed record UpdateAccountProfileCommand(string Email);

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword);

public sealed record RequestPasswordResetCommand(string Email);

public sealed record ResetPasswordWithTokenCommand(
    string Email,
    string Token,
    string NewPassword);

public sealed record AccountSessionDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string AccessToken);

public sealed record AccountProfileDto(
    Guid UserId,
    string DisplayName,
    string Email);
