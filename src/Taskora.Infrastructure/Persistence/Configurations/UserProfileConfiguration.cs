using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class UserProfileConfiguration
    : IEntityTypeConfiguration<UserProfile>
{
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
        builder.HasIndex(user => user.Email).IsUnique();
    }
}
