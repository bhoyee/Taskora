using TodoApp.Application.Accounts;

namespace TodoApp.Api.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/account")
            .WithTags("Account");

        group.MapPost("/register", RegisterAsync)
            .WithName("RegisterAccount");
        group.MapPost("/login", LoginAsync)
            .WithName("Login");

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterAccountRequest request,
        RegisterAccountHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new RegisterAccountCommand(
                request.DisplayName,
                request.Email,
                request.Password,
                request.WorkspaceName),
            cancellationToken));

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken));
}

public sealed record RegisterAccountRequest(
    string DisplayName,
    string Email,
    string Password,
    string WorkspaceName);

public sealed record LoginRequest(string Email, string Password);
