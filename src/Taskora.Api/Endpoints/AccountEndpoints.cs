using TodoApp.Application.Accounts;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Maps account lifecycle endpoints under "/api/v1/account": registration,
/// login, password reset, and profile/password management for the current user.
/// </summary>
internal static class AccountEndpoints
{
    /// <summary>
    /// Registers account routes. Registration, login, and password-reset
    /// routes are anonymous (no prior session is required); "/me", "/profile",
    /// and "/password" each opt into <c>RequireAuthorization()</c> individually
    /// and act on the currently authenticated user.
    /// </summary>
    public static IEndpointRouteBuilder MapAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/account")
            .WithTags("Account");

        // POST /api/v1/account/register: anonymous. Creates a new account
        // and its initial workspace. Returns the outcome via ApiResult
        // (success/validation-error shape depends on the handler result).
        group.MapPost("/register", RegisterAsync)
            .WithName("RegisterAccount");
        // POST /api/v1/account/login: anonymous. Authenticates credentials
        // and returns a session/token payload on success.
        group.MapPost("/login", LoginAsync)
            .WithName("Login");
        // POST /api/v1/account/password/reset/request: anonymous. Kicks off
        // the password-reset flow (e.g. emailing a reset token) for the given address.
        group.MapPost("/password/reset/request", RequestPasswordResetAsync)
            .WithName("RequestPasswordReset");
        // POST /api/v1/account/password/reset/confirm: anonymous. Completes
        // the reset flow by validating the token and setting a new password.
        group.MapPost("/password/reset/confirm", ResetPasswordWithTokenAsync)
            .WithName("ResetPasswordWithToken");
        // GET /api/v1/account/me: requires authentication. Returns the
        // current user's account details.
        group.MapGet("/me", GetCurrentAsync)
            .RequireAuthorization()
            .WithName("GetCurrentAccount");
        // PUT /api/v1/account/profile: requires authentication. Updates the
        // current user's profile (e.g. email).
        group.MapPut("/profile", UpdateProfileAsync)
            .RequireAuthorization()
            .WithName("UpdateAccountProfile");
        // PUT /api/v1/account/password: requires authentication. Changes the
        // current user's password after verifying the current one.
        group.MapPut("/password", ChangePasswordAsync)
            .RequireAuthorization()
            .WithName("ChangePassword");

        return endpoints;
    }

    // Handles POST /register.
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

    // Handles POST /login.
    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken));

    // Handles POST /password/reset/request.
    private static async Task<IResult> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        RequestPasswordResetHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new RequestPasswordResetCommand(request.Email),
            cancellationToken));

    // Handles POST /password/reset/confirm.
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

    // Handles GET /me (authenticated).
    private static async Task<IResult> GetCurrentAsync(
        GetCurrentAccountHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new GetCurrentAccountQuery(),
            cancellationToken));

    // Handles PUT /profile (authenticated).
    private static async Task<IResult> UpdateProfileAsync(
        UpdateAccountProfileRequest request,
        UpdateAccountProfileHandler handler,
        CancellationToken cancellationToken) =>
        ApiResult.From(await handler.HandleAsync(
            new UpdateAccountProfileCommand(request.Email),
            cancellationToken));

    // Handles PUT /password (authenticated).
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

/// <summary>Request body for creating a new account and its initial workspace.</summary>
public sealed record RegisterAccountRequest(
    string DisplayName,
    string Email,
    string Password,
    string WorkspaceName);

/// <summary>Request body for authenticating with an email/password pair.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Request body for starting a password-reset flow for the given email.</summary>
public sealed record RequestPasswordResetRequest(string Email);

/// <summary>Request body for completing a password reset using the emailed token.</summary>
public sealed record ResetPasswordWithTokenRequest(
    string Email,
    string Token,
    string NewPassword);

/// <summary>Request body for updating the current user's profile email.</summary>
public sealed record UpdateAccountProfileRequest(string Email);

/// <summary>Request body for changing the current user's password.</summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
