using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TaskActivity"/> (an append-only audit trail entry for a task) to the
/// "TaskActivities" table. The primary key is a database-generated sequence number rather
/// than a domain-assigned id, since activities are ordered log entries. Deleting a task
/// cascades to delete its activity history.
/// </summary>
internal sealed class TaskActivityConfiguration
    : IEntityTypeConfiguration<TaskActivity>
{
    /// <summary>Configures the audit-log table keyed by an auto-generated sequence number.</summary>
    public void Configure(EntityTypeBuilder<TaskActivity> builder)
    {
        builder.ToTable("TaskActivities");
        builder.HasKey(activity => activity.Sequence);
        // Sequence is a store-generated identity value, giving activities a stable insert order.
        builder.Property(activity => activity.Sequence)
            .ValueGeneratedOnAdd();
        builder.Property(activity => activity.ActivityType)
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(activity => activity.Actor)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(activity => activity.PreviousValue)
            .HasMaxLength(200);
        builder.Property(activity => activity.CurrentValue)
            .HasMaxLength(200);
        // Activity history is owned by the task's lifecycle: removing a task removes its log.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(activity => activity.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        // Supports fetching a task's activity timeline in chronological order.
        builder.HasIndex(activity => new
        {
            activity.TaskId,
            activity.OccurredAt
        });
    }
}
