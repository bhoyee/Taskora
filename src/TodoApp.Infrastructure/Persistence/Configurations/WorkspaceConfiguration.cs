using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceConfiguration
    : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Name)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(workspace => workspace.OwnerId).IsRequired();
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(workspace => workspace.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(workspace => workspace.Memberships);
        builder.HasMany<WorkspaceMembership>("_memberships")
            .WithOne()
            .HasForeignKey(membership => membership.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_memberships")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
