using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceInvitationConfiguration
    : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("WorkspaceInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id)
            .ValueGeneratedNever();
        builder.Property(invitation => invitation.WorkspaceId)
            .IsRequired();
        builder.Property(invitation => invitation.FullName)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(invitation => invitation.Email)
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(invitation => invitation.Role)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(invitation => invitation.InvitedByUserId)
            .IsRequired();
        builder.Property(invitation => invitation.Token)
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(invitation => invitation.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(invitation => invitation.CreatedAt)
            .IsRequired();
        builder.Property(invitation => invitation.ExpiresAt)
            .IsRequired();
        builder.Property(invitation => invitation.RespondedAt);
        builder.HasIndex(invitation => invitation.Token)
            .IsUnique();
        builder.HasIndex(invitation => new
        {
            invitation.WorkspaceId,
            invitation.Email,
            invitation.Status
        });
        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(invitation => invitation.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
