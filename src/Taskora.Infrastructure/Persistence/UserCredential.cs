namespace TodoApp.Infrastructure.Persistence;

public sealed class UserCredential
{
    private UserCredential()
    {
    }

    public UserCredential(Guid userId, string passwordHash)
    {
        UserId = userId;
        PasswordHash = passwordHash;
    }

    public Guid UserId { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public string? PasswordResetTokenHash { get; private set; }

    public DateTimeOffset? PasswordResetTokenExpiresAt { get; private set; }

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        ClearPasswordResetToken();
    }

    public void SetPasswordResetToken(
        string tokenHash,
        DateTimeOffset expiresAt)
    {
        PasswordResetTokenHash = tokenHash;
        PasswordResetTokenExpiresAt = expiresAt;
    }

    public void ClearPasswordResetToken()
    {
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAt = null;
    }
}
