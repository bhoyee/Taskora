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
        group.MapPost("/password/reset/request", RequestPasswordResetAsync)
            .WithName("RequestPasswordReset");
        group.MapPost("/password/reset/confirm", ResetPasswordWithTokenAsync)
            .WithName("ResetPasswordWithToken");
        group.MapGet("/me", GetCurrentAsync)
            .RequireAuthorization()
            .WithName("GetCurrentAccount");
        group.MapPut("/profile", UpdateProfileAsync)
            .RequireAuthorization()
            .WithName("UpdateAccountProfile");
        group.MapPut("/password", ChangePasswordAsync)
            .RequireAuthorization()
            .WithName("ChangePassword");

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

    private static async Task<IResult> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        RequestPasswordResetHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new RequestPasswordResetCommand(request.Email),
            cancellationToken));

    private static async Task<IResult> ResetPasswordWithTokenAsync(
        ResetPasswordWithTokenRequest request,
        ResetPasswordWithTokenHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ResetPasswordWithTokenCommand(
                request.Email,
                request.Token,
                request.NewPassword),
            cancellationToken));

    private static async Task<IResult> GetCurrentAsync(
        GetCurrentAccountHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetCurrentAccountQuery(),
            cancellationToken));

    private static async Task<IResult> UpdateProfileAsync(
        UpdateAccountProfileRequest request,
        UpdateAccountProfileHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new UpdateAccountProfileCommand(request.Email),
            cancellationToken));

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ChangePasswordHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new ChangePasswordCommand(
                request.CurrentPassword,
                request.NewPassword),
            cancellationToken));
}

public sealed record RegisterAccountRequest(
    string DisplayName,
    string Email,
    string Password,
    string WorkspaceName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RequestPasswordResetRequest(string Email);

public sealed record ResetPasswordWithTokenRequest(
    string Email,
    string Token,
    string NewPassword);

public sealed record UpdateAccountProfileRequest(string Email);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
