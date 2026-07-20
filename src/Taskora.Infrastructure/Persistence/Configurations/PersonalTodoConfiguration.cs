using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class PersonalTodoConfiguration
    : IEntityTypeConfiguration<PersonalTodo>
{
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
        builder.Property(todo => todo.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(TodoPriority.Medium)
            .IsRequired();
        builder.Property(todo => todo.DailyRoutineId);
        builder.Property(todo => todo.TodoDate).IsRequired();
        builder.Property(todo => todo.OriginalTodoDate).IsRequired();
        builder.Property(todo => todo.CarriedOverFromDate);
        builder.Property(todo => todo.IsCompleted).IsRequired();
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
        builder.Property(todo => todo.CompletedAt)
            .HasConversion(
                value => value == null
                    ? (long?)null
                    : value.Value.UtcTicks,
                value => value == null
                    ? null
                    : new DateTimeOffset(value.Value, TimeSpan.Zero));
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();

        builder.HasOne<TodoApp.Domain.Collaboration.UserProfile>()
            .WithMany()
            .HasForeignKey(todo => todo.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DailyRoutine>()
            .WithMany()
            .HasForeignKey(todo => todo.DailyRoutineId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(todo => new { todo.UserId, todo.TodoDate });
        builder.HasIndex(todo => new { todo.UserId, todo.IsCompleted });
        builder.HasIndex(todo => new { todo.DailyRoutineId, todo.TodoDate });
        builder.Ignore(todo => todo.IsCarriedOver);
        builder.Ignore(todo => todo.IsGeneratedFromDailyRoutine);
    }
}
