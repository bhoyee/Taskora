namespace TodoApp.Infrastructure.Persistence;

/// <summary>
/// Authentication record for a user: password hash plus an optional,
/// short-lived password-reset token. Kept separate from the domain
/// <c>UserProfile</c> so login/credential concerns stay in the
/// infrastructure layer rather than the domain model.
/// </summary>
public sealed class UserCredential
{
    // Reserved for EF Core materialization.
    private UserCredential()
    {
    }

    public UserCredential(Guid userId, string passwordHash)
    {
        UserId = userId;
        PasswordHash = passwordHash;
    }

    /// <summary>Id of the user this credential belongs to.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Hashed (never plaintext) password.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Hash of the currently outstanding password-reset token, if any.</summary>
    public string? PasswordResetTokenHash { get; private set; }

    /// <summary>Expiry for the outstanding password-reset token, if any.</summary>
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; private set; }

    /// <summary>Replaces the password hash and invalidates any pending reset token.</summary>
    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        ClearPasswordResetToken();
    }

    /// <summary>Records a new password-reset token (as a hash) and its expiry.</summary>
    public void SetPasswordResetToken(
        string tokenHash,
        DateTimeOffset expiresAt)
    {
        PasswordResetTokenHash = tokenHash;
        PasswordResetTokenExpiresAt = expiresAt;
    }

    /// <summary>Clears any outstanding password-reset token, e.g. after use or on password change.</summary>
    public void ClearPasswordResetToken()
    {
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAt = null;
    }
}
