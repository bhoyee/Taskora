using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TaskTag"/> to the "TaskTags" table. Uses a composite primary key of
/// (TaskId, Name) rather than a surrogate id, since a tag is uniquely identified by its
/// name within a task. The relationship back to the owning task is configured on the
/// <see cref="TaskItem"/> side via the "_tags" navigation.
/// </summary>
internal sealed class TaskTagConfiguration : IEntityTypeConfiguration<TaskTag>
{
    /// <summary>Configures the composite key and lookup index for task tags.</summary>
    public void Configure(EntityTypeBuilder<TaskTag> builder)
    {
        builder.ToTable("TaskTags");
        // Composite key: a tag name must be unique per task, and no surrogate id is needed.
        builder.HasKey(tag => new { tag.TaskId, tag.Name });
        builder.Property(tag => tag.Name)
            .HasMaxLength(40)
            .IsRequired();
        // Supports lookups/filters by tag name across tasks (e.g. "find all tasks tagged X").
        builder.HasIndex(tag => tag.Name);
    }
}
