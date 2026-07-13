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
        builder.Property(todo => todo.TodoDate).IsRequired();
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
        builder.HasIndex(todo => new { todo.UserId, todo.TodoDate });
        builder.HasIndex(todo => new { todo.UserId, todo.IsCompleted });
    }
}
