using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Project"/> to the "Projects" table. Owns its <see cref="ProjectCategory"/>
/// and <see cref="Sprint"/> collections (both cascade-deleted with the project) through private
/// backing fields, converts the <see cref="DueDate"/> value object to/from a plain
/// <see cref="DateOnly"/>, and uses a shadow concurrency token for optimistic concurrency.
/// </summary>
internal sealed class ProjectConfiguration
    : IEntityTypeConfiguration<Project>
{
    /// <summary>Configures table mapping, value-object conversions, and the owned categories/sprints collections.</summary>
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Name)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(project => project.WorkspaceId).IsRequired();
        builder.Property(project => project.Description)
            .HasMaxLength(2000);
        // Unwrap the DueDate value object to a plain nullable DateOnly for storage, and
        // reconstruct it via DueDate.Create on read (re-applying any value-object invariants).
        builder.Property(project => project.TargetDate)
            .HasConversion(
                dueDate => dueDate == null
                    ? (DateOnly?)null
                    : dueDate.Value,
                value => value == null
                    ? null
                    : DueDate.Create(value.Value));
        builder.Property(project => project.ArchivedAt);
        // A shadow property (not exposed on the entity) used for optimistic concurrency checks.
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();
        // Computed from ArchivedAt, so it must not be mapped as its own column.
        builder.Ignore(project => project.IsArchived);
        builder.HasIndex(project => project.Name);
        builder.HasIndex(project => project.WorkspaceId);

        // Categories are owned by the project; cascade delete removes them with their parent.
        builder.HasMany<ProjectCategory>("_categories")
            .WithOne()
            .HasForeignKey(category => category.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        // Access the collection through the private field instead of a public property,
        // preserving encapsulation of the aggregate.
        builder.Navigation("_categories")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        // Sprints are likewise owned by the project; cascade delete removes them with their parent.
        builder.HasMany<Sprint>("_sprints")
            .WithOne()
            .HasForeignKey(sprint => sprint.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_sprints")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        // Both are read-only projections over the private fields above, not separate mapped state.
        builder.Ignore(project => project.Categories);
        builder.Ignore(project => project.Sprints);
    }
}
