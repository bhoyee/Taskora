using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="UserProfile"/> to the "UserProfiles" table. Email is enforced unique at the
/// database level since it is used to identify/authenticate users, in addition to any
/// application-level checks.
/// </summary>
internal sealed class UserProfileConfiguration
    : IEntityTypeConfiguration<UserProfile>
{
    /// <summary>Configures table mapping and the unique email constraint.</summary>
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.DisplayName)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(user => user.Email)
            .HasMaxLength(320)
            .IsRequired();
        // Email must be unique so it can reliably identify a user (e.g. during login).
        builder.HasIndex(user => user.Email).IsUnique();
    }
}
