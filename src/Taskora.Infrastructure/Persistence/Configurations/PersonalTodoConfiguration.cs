using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="PersonalTodo"/> to the "PersonalTodos" table. A todo optionally originates
/// from a <see cref="DailyRoutine"/> (nullable FK, set-null on routine delete so the todo
/// survives), and owns a "_comments" collection exposed only through a private backing field.
/// Several read-only/computed properties on the entity are excluded from mapping.
/// </summary>
internal sealed class PersonalTodoConfiguration
    : IEntityTypeConfiguration<PersonalTodo>
{
    /// <summary>Configures table mapping, timestamp/enum conversions, relationships, and the comments collection.</summary>
    public void Configure(EntityTypeBuilder<PersonalTodo> builder)
    {
        builder.ToTable("PersonalTodos");
        builder.HasKey(todo => todo.Id);
        builder.Property(todo => todo.UserId).IsRequired();
        builder.Property(todo => todo.Title)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(todo => todo.Notes)
            .HasMaxLength(1000);
        // Store the priority enum as its string name (not the int value) for readability
        // when inspecting the data directly.
        builder.Property(todo => todo.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(todo => todo.DailyRoutineId);
        builder.Property(todo => todo.TodoDate).IsRequired();
        builder.Property(todo => todo.OriginalTodoDate).IsRequired();
        builder.Property(todo => todo.CarriedOverFromDate);
        builder.Property(todo => todo.IsCompleted).IsRequired();
        // Store DateTimeOffset as raw UTC ticks (long) for portability across SQLite/Postgres,
        // rather than relying on each provider's native DateTimeOffset support.
        builder.Property(todo => todo.CreatedAt)
            .HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        builder.Property(todo => todo.UpdatedAt)
            .HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        // Same UTC-ticks approach as above, but nullable since a todo may not be completed yet.
        builder.Property(todo => todo.CompletedAt)
            .HasConversion(
                value => value == null
                    ? (long?)null
                    : value.Value.UtcTicks,
                value => value == null
                    ? null
                    : new DateTimeOffset(value.Value, TimeSpan.Zero));
        // A shadow property (not exposed on the entity) used for optimistic concurrency checks.
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();

        // A todo is personal to its user: deleting the user removes their todos too.
        builder.HasOne<TodoApp.Domain.Collaboration.UserProfile>()
            .WithMany()
            .HasForeignKey(todo => todo.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        // If the generating routine is deleted, keep the todo but detach it from the routine
        // rather than deleting the todo itself.
        builder.HasOne<DailyRoutine>()
            .WithMany()
            .HasForeignKey(todo => todo.DailyRoutineId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(todo => new { todo.UserId, todo.TodoDate });
        builder.HasIndex(todo => new { todo.UserId, todo.IsCompleted });
        builder.HasIndex(todo => new { todo.DailyRoutineId, todo.TodoDate });

        // Comments are owned by the todo; cascade delete removes them with their parent.
        builder.HasMany<PersonalTodoComment>("_comments")
            .WithOne()
            .HasForeignKey(comment => comment.TodoId)
            .OnDelete(DeleteBehavior.Cascade);
        // Access the collection through the private field instead of a public property,
        // preserving encapsulation of the aggregate.
        builder.Navigation("_comments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // These are computed from other mapped state (or, for Comments, a read-only projection
        // of "_comments") and must not be mapped as their own columns.
        builder.Ignore(todo => todo.IsCarriedOver);
        builder.Ignore(todo => todo.IsGeneratedFromDailyRoutine);
        builder.Ignore(todo => todo.Comments);
    }
}
