using System.Net.Mail;
using System.Security.Cryptography;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Application.Notifications;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Common;

namespace TodoApp.Application.Accounts;

/// <summary>
/// Registers a new user account together with a default (or named) workspace
/// for that user, and returns an authenticated session for the new account.
/// </summary>
public sealed class RegisterAccountHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers)
{
    /// <summary>
    /// Validates the password length and email format, ensures no account
    /// already exists for the email (conflict), then creates a new
    /// <see cref="UserProfile"/> and owning <see cref="Workspace"/> and
    /// persists them. Returns a validation/conflict error on failure, or a
    /// new <see cref="AccountSessionDto"/> on success.
    /// </summary>
    public async Task<Result<AccountSessionDto>> HandleAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Password.Length < 8)
        {
            return Validation("Password must be at least 8 characters.");
        }

        var email = NormalizeEmail(command.Email);
        if (email is null)
        {
            return Validation("A valid email address is required.");
        }

        if (await accounts.EmailExistsAsync(email, cancellationToken))
        {
            return Result<AccountSessionDto>.Failure(
                new ApplicationError(
                    "account.email_exists",
                    "An account already exists for this email.",
                    ErrorType.Conflict));
        }

        try
        {
            var user = UserProfile.Create(
                identifiers.NewId(),
                command.DisplayName,
                email);
            var workspace = Workspace.Create(
                identifiers.NewId(),
                string.IsNullOrWhiteSpace(command.WorkspaceName)
                    ? $"{user.DisplayName}'s workspace"
                    : command.WorkspaceName,
                user.Id);
            await accounts.AddAsync(
                user,
                workspace,
                PasswordHasher.Hash(command.Password),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<AccountSessionDto>.Success(ToSession(user));
        }
        catch (DomainValidationException exception)
        {
            return Validation(exception.Message);
        }
    }

    // Builds a validation-typed failure result for this handler.
    private static Result<AccountSessionDto> Validation(string message) =>
        Result<AccountSessionDto>.Failure(
            new ApplicationError(
                "account.validation",
                message,
                ErrorType.Validation));

    // Maps a user profile to the session DTO returned after authentication.
    internal static AccountSessionDto ToSession(UserProfile user) =>
        new(user.Id, user.DisplayName, user.Email, user.Id.ToString());

    // Trims and lowercases an email address and validates its format,
    // returning null when the address is not a well-formed email.
    internal static string? NormalizeEmail(string email)
    {
        try
        {
            var normalized = email.Trim().ToLowerInvariant();
            return new MailAddress(normalized).Address == normalized
                ? normalized
                : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Authenticates a user by email and password.</summary>
public sealed class LoginHandler(IAccountRepository accounts)
{
    /// <summary>
    /// Normalizes the email, looks up the account, and verifies the password
    /// hash. Returns an <see cref="ErrorType.Unauthorized"/> failure for any
    /// invalid email/password combination (without distinguishing which was
    /// wrong), or a new <see cref="AccountSessionDto"/> on success.
    /// </summary>
    public async Task<Result<AccountSessionDto>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var email = RegisterAccountHandler.NormalizeEmail(command.Email);
        if (email is null)
        {
            return Invalid();
        }

        var account = await accounts.GetByEmailAsync(email, cancellationToken);
        if (account is null ||
            !PasswordHasher.Verify(command.Password, account.PasswordHash))
        {
            return Invalid();
        }

        return Result<AccountSessionDto>.Success(
            RegisterAccountHandler.ToSession(account.User));
    }

    // Builds the generic "invalid credentials" failure, deliberately not
    // revealing whether the email or the password was the problem.
    private static Result<AccountSessionDto> Invalid() =>
        Result<AccountSessionDto>.Failure(
            new ApplicationError(
                "account.invalid_login",
                "Email or password is incorrect.",
                ErrorType.Unauthorized));
}

/// <summary>Retrieves the profile of the currently authenticated user.</summary>
public sealed class GetCurrentAccountHandler(
    IAccountRepository accounts,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires the caller to be authenticated and the corresponding account
    /// to still exist, returning <see cref="ErrorType.Unauthorized"/> or
    /// <see cref="ErrorType.NotFound"/> respectively, otherwise the account's
    /// <see cref="AccountProfileDto"/>.
    /// </summary>
    public async Task<Result<AccountProfileDto>> HandleAsync(
        GetCurrentAccountQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Unauthorized<AccountProfileDto>();
        }

        var account = await accounts.GetByIdAsync(
            currentUser.UserId,
            cancellationToken);
        return account is null
            ? Result<AccountProfileDto>.Failure(
                new ApplicationError(
                    "account.not_found",
                    "The account was not found.",
                    ErrorType.NotFound))
            : Result<AccountProfileDto>.Success(ToProfile(account.User));
    }

    // Maps a user profile to its DTO representation.
    internal static AccountProfileDto ToProfile(UserProfile user) =>
        new(user.Id, user.DisplayName, user.Email);

    // Shared "authentication required" failure reused by other account handlers.
    internal static Result<T> Unauthorized<T>() =>
        Result<T>.Failure(new ApplicationError(
            "identity.unauthorized",
            "Authentication is required.",
            ErrorType.Unauthorized));
}

/// <summary>Updates the current user's account email address.</summary>
public sealed class UpdateAccountProfileHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication and an existing account, validates the new
    /// email format, and rejects the change with
    /// <see cref="ErrorType.Conflict"/> if another account already owns that
    /// email. On success, updates the email and returns the refreshed
    /// <see cref="AccountProfileDto"/>.
    /// </summary>
    public async Task<Result<AccountProfileDto>> HandleAsync(
        UpdateAccountProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return GetCurrentAccountHandler.Unauthorized<AccountProfileDto>();
        }

        var account = await accounts.GetByIdAsync(
            currentUser.UserId,
            cancellationToken);
        if (account is null)
        {
            return Result<AccountProfileDto>.Failure(
                new ApplicationError(
                    "account.not_found",
                    "The account was not found.",
                    ErrorType.NotFound));
        }

        var email = RegisterAccountHandler.NormalizeEmail(command.Email);
        if (email is null)
        {
            return Validation<AccountProfileDto>(
                "A valid email address is required.");
        }

        if (email != account.User.Email &&
            await accounts.EmailExistsAsync(email, cancellationToken))
        {
            return Result<AccountProfileDto>.Failure(
                new ApplicationError(
                    "account.email_exists",
                    "An account already exists for this email.",
                    ErrorType.Conflict));
        }

        try
        {
            account.User.UpdateEmail(email);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AccountProfileDto>.Success(
                GetCurrentAccountHandler.ToProfile(account.User));
        }
        catch (DomainValidationException exception)
        {
            return Validation<AccountProfileDto>(exception.Message);
        }
    }

    // Builds a validation-typed failure result for this handler.
    private static Result<T> Validation<T>(string message) =>
        Result<T>.Failure(
            new ApplicationError(
                "account.validation",
                message,
                ErrorType.Validation));
}

/// <summary>Changes the current user's password after verifying their current one.</summary>
public sealed class ChangePasswordHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Requires authentication, validates the new password's minimum length,
    /// and verifies the supplied current password against the stored hash
    /// (<see cref="ErrorType.Unauthorized"/> if it does not match) before
    /// persisting the new password hash.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return GetCurrentAccountHandler.Unauthorized<bool>();
        }

        if (command.NewPassword.Length < 8)
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "account.validation",
                    "Password must be at least 8 characters.",
                    ErrorType.Validation));
        }

        var account = await accounts.GetByIdAsync(
            currentUser.UserId,
            cancellationToken);
        if (account is null ||
            !PasswordHasher.Verify(
                command.CurrentPassword,
                account.PasswordHash))
        {
            return Result<bool>.Failure(
                new ApplicationError(
                    "account.invalid_password",
                    "Current password is incorrect.",
                    ErrorType.Unauthorized));
        }

        await accounts.ChangePasswordAsync(
            currentUser.UserId,
            PasswordHasher.Hash(command.NewPassword),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

/// <summary>
/// Issues a short-lived password reset code and emails it to the account
/// owner, if an account exists for the given email.
/// </summary>
public sealed class RequestPasswordResetHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IBackgroundEmailDispatcher emailDispatcher,
    IClock clock)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Always returns success (to avoid revealing whether an email is
    /// registered) regardless of whether the email is malformed or has no
    /// matching account. When a match is found, generates and stores a
    /// hashed reset token with a 15-minute expiry and dispatches the
    /// plaintext code to the account owner in the background - the response
    /// never waits on the email actually being delivered, both so a slow
    /// SMTP round-trip can't hang the request and so response timing alone
    /// can't be used to infer whether the email was registered.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        var email = RegisterAccountHandler.NormalizeEmail(command.Email);
        if (email is null)
        {
            return Result<bool>.Success(true);
        }

        var account = await accounts.GetByEmailAsync(email, cancellationToken);
        if (account is null)
        {
            return Result<bool>.Success(true);
        }

        var token = PasswordResetTokenGenerator.Generate();
        await accounts.SetPasswordResetTokenAsync(
            account.User.Id,
            PasswordHasher.Hash(token),
            clock.UtcNow.Add(TokenLifetime),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        emailDispatcher.Dispatch(BuildResetEmail(account.User.Email, token));

        return Result<bool>.Success(true);
    }

    // Composes the password reset email containing the plaintext reset code.
    private static NotificationEmailMessage BuildResetEmail(
        string email,
        string token) =>
        TaskoraEmailTemplate.Build(
            [email],
            "Taskora password reset code",
            "Account security",
            "Your password reset code",
            "Hello,",
            "A password reset was requested for your Taskora account.",
            [
                new EmailDetail("Reset code", token),
                new EmailDetail("Expires in", "15 minutes")
            ],
            "Enter this code on the password reset screen to continue.",
            secondaryNote: "If you did not request this, you can safely ignore this email.");
}

/// <summary>Completes a password reset using a previously issued reset code.</summary>
public sealed class ResetPasswordWithTokenHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>
    /// Validates the new password length and the token's format, then
    /// verifies the token against the stored hash and expiry
    /// (<see cref="ErrorType.Validation"/> failure if invalid, unmatched, or
    /// expired). On success, updates the account's password hash.
    /// </summary>
    public async Task<Result<bool>> HandleAsync(
        ResetPasswordWithTokenCommand command,
        CancellationToken cancellationToken)
    {
        if (command.NewPassword.Length < 8)
        {
            return Validation(
                "Password must be at least 8 characters.");
        }

        var email = RegisterAccountHandler.NormalizeEmail(command.Email);
        if (email is null)
        {
            return InvalidToken();
        }

        var token = NormalizeToken(command.Token);
        if (token is null)
        {
            return InvalidToken();
        }

        var account = await accounts.GetByEmailAsync(email, cancellationToken);
        if (account is null ||
            string.IsNullOrWhiteSpace(account.PasswordResetTokenHash) ||
            account.PasswordResetTokenExpiresAt is null ||
            account.PasswordResetTokenExpiresAt <= clock.UtcNow ||
            !PasswordHasher.Verify(token, account.PasswordResetTokenHash))
        {
            return InvalidToken();
        }

        await accounts.ChangePasswordAsync(
            account.User.Id,
            PasswordHasher.Hash(command.NewPassword),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    // Trims the token and validates it is a 6-digit numeric code.
    private static string? NormalizeToken(string token)
    {
        var normalized = token.Trim();
        return normalized.Length == 6 &&
            normalized.All(char.IsDigit)
                ? normalized
                : null;
    }

    // Builds the "invalid or expired reset code" failure.
    private static Result<bool> InvalidToken() =>
        Result<bool>.Failure(
            new ApplicationError(
                "account.invalid_reset_token",
                "The reset code is invalid or has expired.",
                ErrorType.Validation));

    // Builds a validation-typed failure result for this handler.
    private static Result<bool> Validation(string message) =>
        Result<bool>.Failure(
            new ApplicationError(
                "account.validation",
                message,
                ErrorType.Validation));
}

/// <summary>Generates random numeric password reset codes.</summary>
internal static class PasswordResetTokenGenerator
{
    /// <summary>Generates a random 6-digit numeric reset code.</summary>
    public static string Generate()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}

/// <summary>Hashes and verifies passwords using PBKDF2 (Rfc2898DeriveBytes).</summary>
internal static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    /// <summary>
    /// Hashes a password with a fresh random salt using PBKDF2/SHA-256, and
    /// returns the iteration count, salt, and hash encoded together as a
    /// single dot-delimited string suitable for storage.
    /// </summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a plaintext password against a stored hash produced by
    /// <see cref="Hash"/>, using a fixed-time comparison to avoid timing
    /// attacks.
    /// </summary>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
