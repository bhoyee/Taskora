using System.Net.Mail;
using TodoApp.Domain.Common;

namespace TodoApp.Domain.Collaboration;

public sealed class UserProfile
{
    private UserProfile(Guid id, string displayName, string email)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "User identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainValidationException(
                "Display name is required.");
        }

        Id = id;
        DisplayName = displayName.Trim();
        Email = NormalizeEmail(email);
    }

    public Guid Id { get; }

    public string DisplayName { get; private set; }

    public string Email { get; private set; }

    public static UserProfile Create(
        Guid id,
        string displayName,
        string email) =>
        new(id, displayName, email);

    private static string NormalizeEmail(string email)
    {
        try
        {
            var normalized = email.Trim().ToLowerInvariant();
            var address = new MailAddress(normalized);
            if (address.Address != normalized)
            {
                throw new FormatException();
            }

            return normalized;
        }
        catch (Exception exception)
            when (exception is FormatException or ArgumentException)
        {
            throw new DomainValidationException(
                "A valid email address is required.");
        }
    }
}
