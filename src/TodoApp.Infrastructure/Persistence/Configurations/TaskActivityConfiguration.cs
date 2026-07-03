using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class TaskActivityConfiguration
    : IEntityTypeConfiguration<TaskActivity>
{
    public void Configure(EntityTypeBuilder<TaskActivity> builder)
    {
        builder.ToTable("TaskActivities");
        builder.HasKey(activity => activity.Sequence);
        builder.Property(activity => activity.Sequence)
            .ValueGeneratedOnAdd();
        builder.Property(activity => activity.ActivityType)
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(activity => activity.PreviousValue)
            .HasMaxLength(200);
        builder.Property(activity => activity.CurrentValue)
            .HasMaxLength(200);
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(activity => activity.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(activity => new
        {
            activity.TaskId,
            activity.OccurredAt
        });
    }
}
