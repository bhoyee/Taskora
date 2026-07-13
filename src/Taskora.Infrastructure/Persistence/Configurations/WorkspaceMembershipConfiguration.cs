using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMembershipConfiguration
    : IEntityTypeConfiguration<WorkspaceMembership>
{
    public void Configure(EntityTypeBuilder<WorkspaceMembership> builder)
    {
        builder.ToTable("WorkspaceMemberships");
        builder.HasKey(membership => new
        {
            membership.WorkspaceId,
            membership.UserId
        });
        builder.Property(membership => membership.Role)
            .HasConversion<int>()
            .IsRequired();
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(membership => new
        {
            membership.UserId,
            membership.WorkspaceId
        });
    }
}
