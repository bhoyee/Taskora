using TodoApp.Application.Abstractions;
using TodoApp.Application.PublicDemo;

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
    // combining the multi-value "SuperAdminEmails" list, the legacy
    // single-value "SuperAdminEmail" setting, and the public demo's fixed
    // Super Admin persona (always recognized, no config needed, so a fresh
    // deployment doesn't need any manual step for the demo to work) —
    // case-insensitively. The demo persona's actual admin *powers* are
    // separately restricted by PublicDemoIdentifiers.AllowsDestructiveBypass
    // wherever a super admin can delete/suspend a workspace, so recognizing
    // it here only grants read-only visibility (Platform/Operations pages).
    public static bool IsSuperAdmin(
        string email,
        IConfiguration configuration)
    {
        if (email.Equals(
                PublicDemoIdentifiers.SuperAdminEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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
