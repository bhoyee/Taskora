using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class TaskNoteConfiguration : IEntityTypeConfiguration<TaskNote>
{
    public void Configure(EntityTypeBuilder<TaskNote> builder)
    {
        builder.ToTable("TaskNotes");
        builder.HasKey(note => note.Id);
        builder.Property(note => note.Id)
            .ValueGeneratedNever();
        builder.Property(note => note.TaskId).IsRequired();
        builder.Property(note => note.AuthorId).IsRequired();
        builder.Property(note => note.Body)
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(note => note.CreatedAt)
            .HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        builder.HasIndex(note => new { note.TaskId, note.CreatedAt });
    }
}
