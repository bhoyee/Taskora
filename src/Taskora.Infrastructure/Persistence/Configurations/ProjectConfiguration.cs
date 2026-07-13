using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration
    : IEntityTypeConfiguration<Project>
{
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
        builder.Property(project => project.TargetDate)
            .HasConversion(
                dueDate => dueDate == null
                    ? (DateOnly?)null
                    : dueDate.Value,
                value => value == null
                    ? null
                    : DueDate.Create(value.Value));
        builder.Property(project => project.ArchivedAt);
        builder.Property<Guid>("ConcurrencyToken")
            .IsConcurrencyToken();
        builder.Ignore(project => project.IsArchived);
        builder.HasIndex(project => project.Name);
        builder.HasIndex(project => project.WorkspaceId);

        builder.HasMany<ProjectCategory>("_categories")
            .WithOne()
            .HasForeignKey(category => category.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_categories")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(project => project.Categories);
    }
}
