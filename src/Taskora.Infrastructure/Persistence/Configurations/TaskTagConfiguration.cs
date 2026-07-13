using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class TaskTagConfiguration : IEntityTypeConfiguration<TaskTag>
{
    public void Configure(EntityTypeBuilder<TaskTag> builder)
    {
        builder.ToTable("TaskTags");
        builder.HasKey(tag => new { tag.TaskId, tag.Name });
        builder.Property(tag => tag.Name)
            .HasMaxLength(40)
            .IsRequired();
        builder.HasIndex(tag => tag.Name);
    }
}
