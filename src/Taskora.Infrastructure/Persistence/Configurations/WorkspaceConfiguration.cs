using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Workspace"/> to the "Workspaces" table. Uses a shadow concurrency token to
/// guard against lost updates, and exposes its memberships as a private backing-field
/// collection (<c>_memberships</c>) rather than a settable navigation property, since
/// <see cref="Workspace.Memberships"/> is a read-only projection over that field.
/// </summary>
internal sealed class WorkspaceConfiguration
    : IEntityTypeConfiguration<Workspace>
{
    /// <summary>Configures table mapping, owner/suspension relationships, and the memberships collection.</summary>
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Name)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(workspace => workspace.OwnerId).IsRequired();
        builder.Property(workspace => workspace.SuspendedAt);
        builder.Property(workspace => workspace.SuspendedByUserId);
        builder.Property(workspace => workspace.SuspendedReason)
            .HasMaxLength(500);
        // A shadow property (not exposed on the entity) used for optimistic concurrency checks.
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();
        // Restrict rather than cascade: the owning user cannot be deleted while they own a workspace.
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(workspace => workspace.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        // If the admin who suspended a workspace is later deleted, clear the reference rather
        // than blocking the delete or cascading the workspace away.
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(workspace => workspace.SuspendedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        // Memberships is a computed/read-only view over the "_memberships" backing field below,
        // so it is excluded from mapping in favor of the field-backed navigation.
        builder.Ignore(workspace => workspace.Memberships);
        builder.HasMany<WorkspaceMembership>("_memberships")
            .WithOne()
            .HasForeignKey(membership => membership.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        // Access the collection through the private field instead of a public property,
        // preserving encapsulation of the aggregate.
        builder.Navigation("_memberships")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
