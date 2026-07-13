using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class UserCredentialConfiguration
    : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("UserCredentials");
        builder.HasKey(credential => credential.UserId);
        builder.Property(credential => credential.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(credential => credential.PasswordResetTokenHash)
            .HasMaxLength(512);
        builder.Property(credential => credential.PasswordResetTokenExpiresAt);
    }
}
