using TodoApp.Application.Abstractions;

namespace TodoApp.Api.Authorization;

/// <summary>
/// Determines whether a given user is a super-admin, based on a
/// configuration-driven allowlist of emails rather than a stored role — this
/// lets deployments grant super-admin access via app settings without a
/// database migration.
/// </summary>
internal static class SuperAdminAuthorization
{
    // Checks whether the given email matches any configured super-admin email,
    // combining both the multi-value "SuperAdminEmails" list and the legacy
    // single-value "SuperAdminEmail" setting, case-insensitively.
    public static bool IsSuperAdmin(
        string email,
        IConfiguration configuration)
    {
        var emails = configuration
            .GetSection("Administration:SuperAdminEmails")
            .Get<string[]>() ?? [];
        var singleEmail = configuration["Administration:SuperAdminEmail"];
        if (!string.IsNullOrWhiteSpace(singleEmail))
        {
            emails = [.. emails, singleEmail];
        }

        return emails.Any(candidate =>
            email.Equals(
                candidate?.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves the current user's account and checks its email against the
    /// configured super-admin allowlist. Returns false if the account can't be found.
    /// </summary>
    public static async Task<bool> IsSuperAdminAsync(
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(
            currentUser.UserId,
            cancellationToken);
        return account is not null &&
            IsSuperAdmin(account.User.Email, configuration);
    }
}
