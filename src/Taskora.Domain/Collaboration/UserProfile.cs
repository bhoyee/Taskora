using System.Net.Mail;
using TodoApp.Domain.Common;

namespace TodoApp.Domain.Collaboration;

/// <summary>
/// Aggregate root representing a user's identity within the system: their display
/// name and a validated, normalized email address.
/// </summary>
public sealed class UserProfile
{
    // Reserved for ORM materialization; domain code must use the factory method.
    private UserProfile()
    {
    }

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

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    /// <summary>Creates a new user profile with a validated display name and email.</summary>
    public static UserProfile Create(
        Guid id,
        string displayName,
        string email) =>
        new(id, displayName, email);

    /// <summary>Changes the user's email address, applying the same normalization/validation as creation.</summary>
    public void UpdateEmail(string email)
    {
        Email = NormalizeEmail(email);
    }

    // Lowercases and trims the email, then uses MailAddress to confirm it parses as a
    // valid address and round-trips exactly (guarding against e.g. embedded display names).
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
