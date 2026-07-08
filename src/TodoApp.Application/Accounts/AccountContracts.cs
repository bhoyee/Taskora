namespace TodoApp.Application.Accounts;

public sealed record RegisterAccountCommand(
    string DisplayName,
    string Email,
    string Password,
    string WorkspaceName);

public sealed record LoginCommand(string Email, string Password);

public sealed record AccountSessionDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string AccessToken);
