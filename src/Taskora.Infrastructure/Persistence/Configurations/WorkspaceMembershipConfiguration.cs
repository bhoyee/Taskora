using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="WorkspaceMembership"/> (the join entity between a user and a workspace,
/// carrying the user's role) to the "WorkspaceMemberships" table. Uses a composite primary
/// key of (WorkspaceId, UserId) since a user can only have one membership per workspace.
/// The cascade-on-delete-of-workspace side of this relationship is configured on
/// <see cref="WorkspaceConfiguration"/> via the "_memberships" navigation.
/// </summary>
internal sealed class WorkspaceMembershipConfiguration
    : IEntityTypeConfiguration<WorkspaceMembership>
{
    /// <summary>Configures the composite key, role enum conversion, and lookup index.</summary>
    public void Configure(EntityTypeBuilder<WorkspaceMembership> builder)
    {
        builder.ToTable("WorkspaceMemberships");
        // Composite key: a user has at most one membership (and role) per workspace.
        builder.HasKey(membership => new
        {
            membership.WorkspaceId,
            membership.UserId
        });
        // Store the enum as its underlying int rather than a string for compactness/stability.
        builder.Property(membership => membership.Role)
            .HasConversion<int>()
            .IsRequired();
        // Restrict rather than cascade: a user with active memberships cannot be deleted outright.
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        // Supports the common "list a user's workspaces" query (reverse of the primary key order).
        builder.HasIndex(membership => new
        {
            membership.UserId,
            membership.WorkspaceId
        });
    }
}
