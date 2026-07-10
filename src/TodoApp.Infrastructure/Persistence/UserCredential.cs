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

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }
}
