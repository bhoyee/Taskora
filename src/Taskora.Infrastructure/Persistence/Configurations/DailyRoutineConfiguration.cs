using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="DailyRoutine"/> (a recurring template that generates <see cref="PersonalTodo"/>
/// instances) to the "DailyRoutines" table. Deleting the owning user cascades to delete their
/// routines, and a shadow concurrency token guards against lost updates.
/// </summary>
internal sealed class DailyRoutineConfiguration
    : IEntityTypeConfiguration<DailyRoutine>
{
    /// <summary>Configures table mapping, enum/timestamp conversions, and the owning-user relationship.</summary>
    public void Configure(EntityTypeBuilder<DailyRoutine> builder)
    {
        builder.ToTable("DailyRoutines");
        builder.HasKey(routine => routine.Id);
        builder.Property(routine => routine.UserId).IsRequired();
        builder.Property(routine => routine.Title)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(routine => routine.Notes)
            .HasMaxLength(1000);
        // Store the priority enum as its string name (not the int value) for readability
        // when inspecting the data directly.
        builder.Property(routine => routine.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(routine => routine.StartDate).IsRequired();
        builder.Property(routine => routine.EndDate);
        builder.Property(routine => routine.IsActive).IsRequired();
        builder.Property(routine => routine.LastGeneratedDate);
        // Store DateTimeOffset as raw UTC ticks (long) for portability across SQLite/Postgres,
        // rather than relying on each provider's native DateTimeOffset support.
        builder.Property(routine => routine.CreatedAt)
            .HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        builder.Property(routine => routine.UpdatedAt)
            .HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        // A shadow property (not exposed on the entity) used for optimistic concurrency checks.
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();

        // A routine is personal to its user: deleting the user removes their routines too.
        builder.HasOne<TodoApp.Domain.Collaboration.UserProfile>()
            .WithMany()
            .HasForeignKey(routine => routine.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(routine => new { routine.UserId, routine.IsActive });
        builder.HasIndex(routine => new { routine.UserId, routine.StartDate });
    }
}
