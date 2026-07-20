using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class DailyRoutineConfiguration
    : IEntityTypeConfiguration<DailyRoutine>
{
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
        builder.Property(routine => routine.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(routine => routine.StartDate).IsRequired();
        builder.Property(routine => routine.EndDate);
        builder.Property(routine => routine.IsActive).IsRequired();
        builder.Property(routine => routine.LastGeneratedDate);
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
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();

        builder.HasOne<TodoApp.Domain.Collaboration.UserProfile>()
            .WithMany()
            .HasForeignKey(routine => routine.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(routine => new { routine.UserId, routine.IsActive });
        builder.HasIndex(routine => new { routine.UserId, routine.StartDate });
    }
}
