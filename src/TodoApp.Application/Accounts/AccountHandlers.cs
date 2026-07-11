using System.Net.Mail;
using System.Security.Cryptography;
using TodoApp.Application.Abstractions;
using TodoApp.Application.Common;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Common;

namespace TodoApp.Application.Accounts;

public sealed class RegisterAccountHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IIdentifierGenerator identifiers)
{
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

    private static Result<AccountSessionDto> Validation(string message) =>
        Result<AccountSessionDto>.Failure(
            new ApplicationError(
                "account.validation",
                message,
                ErrorType.Validation));

    internal static AccountSessionDto ToSession(UserProfile user) =>
        new(user.Id, user.DisplayName, user.Email, user.Id.ToString());

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

public sealed class LoginHandler(IAccountRepository accounts)
{
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

    private static Result<AccountSessionDto> Invalid() =>
        Result<AccountSessionDto>.Failure(
            new ApplicationError(
                "account.invalid_login",
                "Email or password is incorrect.",
                ErrorType.Unauthorized));
}

public sealed class GetCurrentAccountHandler(
    IAccountRepository accounts,
    ICurrentUser currentUser)
{
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

    internal static AccountProfileDto ToProfile(UserProfile user) =>
        new(user.Id, user.DisplayName, user.Email);

    internal static Result<T> Unauthorized<T>() =>
        Result<T>.Failure(new ApplicationError(
            "identity.unauthorized",
            "Authentication is required.",
            ErrorType.Unauthorized));
}

public sealed class UpdateAccountProfileHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
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

    private static Result<T> Validation<T>(string message) =>
        Result<T>.Failure(
            new ApplicationError(
                "account.validation",
                message,
                ErrorType.Validation));
}

public sealed class ChangePasswordHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
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

internal static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

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
