using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration
    : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.ProjectId).IsRequired();
        builder.Property(task => task.Title)
            .HasMaxLength(240)
            .IsRequired();
        builder.Property(task => task.CreatedAt)
            .HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        builder.Property(task => task.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(task => task.BlockedReason)
            .HasMaxLength(1000);
        builder.Property(task => task.CompletedAt);
        builder.Property(task => task.AssignedUserId);
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();
        builder.Property(task => task.DueDate)
            .HasConversion(
                dueDate => dueDate == null
                    ? (DateOnly?)null
                    : dueDate.Value,
                value => value == null
                    ? null
                    : DueDate.Create(value.Value));
        builder.Property(task => task.EffortEstimate)
            .HasConversion(
                effort => effort == null
                    ? (int?)null
                    : effort.Value,
                value => value == null
                    ? null
                    : EffortEstimate.Create(value.Value));

        builder.OwnsOne<PlanningFactors>(
            "_planningFactors",
            planning =>
            {
                planning.Property(factors => factors.BusinessValue)
                    .HasColumnName("BusinessValue");
                planning.Property(factors => factors.Urgency)
                    .HasColumnName("Urgency");
                planning.Property(factors => factors.RiskReduction)
                    .HasColumnName("RiskReduction");
                planning.Property(factors => factors.EffortEstimate)
                    .HasConversion(
                        effort => effort.Value,
                        value => EffortEstimate.Create(value))
                    .HasColumnName("PlanningEffort");
            });
        builder.Navigation("_planningFactors").IsRequired(false);

        builder.OwnsOne<PriorityScore>(
            "_priority",
            priority =>
            {
                priority.Property(score => score.BusinessValueContribution)
                    .HasColumnName("BusinessValueContribution");
                priority.Property(score => score.UrgencyContribution)
                    .HasColumnName("UrgencyContribution");
                priority.Property(score => score.RiskReductionContribution)
                    .HasColumnName("RiskReductionContribution");
                priority.Property(score => score.Value)
                    .HasPrecision(10, 2)
                    .HasColumnName("PriorityScore");
                priority.Property(score => score.Band)
                    .HasConversion<int>()
                    .HasColumnName("PriorityBand");
            });
        builder.Navigation("_priority").IsRequired(false);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(task => new { task.ProjectId, task.Status });
        builder.HasIndex(task => task.DueDate);
        builder.HasIndex(task => task.CreatedAt);
        builder.HasIndex(task => task.AssignedUserId);

        builder.HasMany<TaskItem>("_dependencies")
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "TaskDependencies",
                right => right
                    .HasOne<TaskItem>()
                    .WithMany()
                    .HasForeignKey("DependencyId")
                    .OnDelete(DeleteBehavior.Restrict),
                left => left
                    .HasOne<TaskItem>()
                    .WithMany()
                    .HasForeignKey("TaskId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("TaskDependencies");
                    join.HasKey("TaskId", "DependencyId");
                    join.HasIndex("DependencyId");
                });
        builder.Navigation("_dependencies")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(task => task.DependencyIds);
        builder.Ignore(task => task.IncompleteDependencyChainIds);
        builder.Ignore(task => task.HasIncompleteDependencies);
        builder.Ignore(task => task.IsBlocked);
        builder.Ignore(task => task.DomainEvents);
        builder.Ignore(task => task.PlanningFactors);
        builder.Ignore(task => task.Priority);
        builder.Ignore(task => task.HasPlanningFactors);
    }
}
