using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TaskItem"/> — the central aggregate root of the domain — to the "Tasks"
/// table. Configures value-object conversions (<see cref="DueDate"/>, <see cref="EffortEstimate"/>),
/// two owned sub-objects (<see cref="PlanningFactors"/> and <see cref="PriorityScore"/>) stored
/// inline in the same table, a self-referencing many-to-many "depends on" relationship via an
/// explicit join table, and owned collections of tags/notes exposed only through private
/// backing fields. Numerous computed properties (dependency lookups, blocked status, domain
/// events, etc.) are excluded from mapping.
/// </summary>
internal sealed class TaskItemConfiguration
    : IEntityTypeConfiguration<TaskItem>
{
    /// <summary>
    /// Configures the Tasks table: keys, value-object conversions, owned planning/priority
    /// sub-objects, project/sprint relationships, the task-dependency join table, and the
    /// owned tags/notes collections.
    /// </summary>
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.ProjectId).IsRequired();
        builder.Property(task => task.Title)
            .HasMaxLength(240)
            .IsRequired();
        // Store DateTimeOffset as raw UTC ticks (long) for portability across SQLite/Postgres,
        // rather than relying on each provider's native DateTimeOffset support.
        builder.Property(task => task.CreatedAt)
            .HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        // Store the enum as its underlying int rather than a string for compactness/stability.
        builder.Property(task => task.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(task => task.BlockedReason)
            .HasMaxLength(1000);
        builder.Property(task => task.CompletedAt);
        builder.Property(task => task.AssignedUserId);
        builder.Property(task => task.CreatedByUserId);
        builder.Property(task => task.CategoryId);
        builder.Property(task => task.SprintId);
        // A shadow property (not exposed on the entity) used for optimistic concurrency checks.
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();
        // Unwrap the DueDate value object to a plain nullable DateOnly for storage, and
        // reconstruct it via DueDate.Create on read (re-applying any value-object invariants).
        builder.Property(task => task.DueDate)
            .HasConversion(
                dueDate => dueDate == null
                    ? (DateOnly?)null
                    : dueDate.Value,
                value => value == null
                    ? null
                    : DueDate.Create(value.Value));
        // Same unwrap/reconstruct pattern for the EffortEstimate value object, stored as a plain int.
        builder.Property(task => task.EffortEstimate)
            .HasConversion(
                effort => effort == null
                    ? (int?)null
                    : effort.Value,
                value => value == null
                    ? null
                    : EffortEstimate.Create(value.Value));

        // PlanningFactors is an owned type stored inline in the Tasks table (not a separate
        // table), accessed via the private "_planningFactors" field. It is optional per task.
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
                // Nested value-object conversion, same pattern as EffortEstimate above.
                planning.Property(factors => factors.EffortEstimate)
                    .HasConversion(
                        effort => effort.Value,
                        value => EffortEstimate.Create(value))
                    .HasColumnName("PlanningEffort");
            });
        // A task may not have planning factors set yet.
        builder.Navigation("_planningFactors").IsRequired(false);

        // PriorityScore is likewise an owned type stored inline, computed from the planning
        // factors above; also optional and accessed via a private field.
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

        // Restrict rather than cascade: a project with tasks cannot be deleted outright.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        // If the sprint is deleted, unassign the task from it rather than deleting the task.
        builder.HasOne<Sprint>()
            .WithMany()
            .HasForeignKey(task => task.SprintId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(task => new { task.ProjectId, task.Status });
        builder.HasIndex(task => task.DueDate);
        builder.HasIndex(task => task.CreatedAt);
        builder.HasIndex(task => task.AssignedUserId);
        builder.HasIndex(task => task.CategoryId);
        builder.HasIndex(task => task.SprintId);

        // Self-referencing many-to-many "task depends on task" relationship, backed by an
        // explicit join table (rather than a skip-navigation shared type) so we can name the
        // table/columns and set differing delete behaviors on each side.
        builder.HasMany<TaskItem>("_dependencies")
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "TaskDependencies",
                // Deleting the depended-upon task is restricted so dangling dependency
                // references can't silently appear.
                right => right
                    .HasOne<TaskItem>()
                    .WithMany()
                    .HasForeignKey("DependencyId")
                    .OnDelete(DeleteBehavior.Restrict),
                // Deleting the dependent task cascades to remove its dependency rows.
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
        // Access the collection through the private field instead of a public property,
        // preserving encapsulation of the aggregate.
        builder.Navigation("_dependencies")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Tags are owned by the task; cascade delete removes them with their parent.
        builder.HasMany<TaskTag>("_tags")
            .WithOne()
            .HasForeignKey(tag => tag.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_tags")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Notes are likewise owned by the task; cascade delete removes them with their parent.
        builder.HasMany<TaskNote>("_notes")
            .WithOne()
            .HasForeignKey(note => note.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_notes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // These are all computed in-memory (from dependencies, owned sub-objects, or domain
        // event tracking) rather than persisted columns, so they must be excluded from mapping.
        builder.Ignore(task => task.DependencyIds);
        builder.Ignore(task => task.IncompleteDependencyChainIds);
        builder.Ignore(task => task.HasIncompleteDependencies);
        builder.Ignore(task => task.IsBlocked);
        builder.Ignore(task => task.DomainEvents);
        builder.Ignore(task => task.PlanningFactors);
        builder.Ignore(task => task.Priority);
        builder.Ignore(task => task.HasPlanningFactors);
        builder.Ignore(task => task.Tags);
        builder.Ignore(task => task.Notes);
    }
}
