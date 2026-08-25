using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="UserCredential"/> to the "UserCredentials" table. The primary key is the
/// owning user's id (a 1:1 relationship with the user), and only hashed values (password hash,
/// password reset token hash) are persisted—never plaintext secrets.
/// </summary>
internal sealed class UserCredentialConfiguration
    : IEntityTypeConfiguration<UserCredential>
{
    /// <summary>Configures the credential table keyed by user id and its hashed secret fields.</summary>
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("UserCredentials");
        // UserId doubles as the primary key since each user has exactly one credential record.
        builder.HasKey(credential => credential.UserId);
        builder.Property(credential => credential.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(credential => credential.PasswordResetTokenHash)
            .HasMaxLength(512);
        builder.Property(credential => credential.PasswordResetTokenExpiresAt);
    }
}
