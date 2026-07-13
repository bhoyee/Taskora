using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Projects;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class ProjectCategoryConfiguration
    : IEntityTypeConfiguration<ProjectCategory>
{
    public void Configure(EntityTypeBuilder<ProjectCategory> builder)
    {
        builder.ToTable("ProjectCategories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id)
            .ValueGeneratedNever();
        builder.Property(category => category.ProjectId).IsRequired();
        builder.Property(category => category.Name)
            .HasMaxLength(80)
            .IsRequired();
        builder.HasIndex(category => new { category.ProjectId, category.Name })
            .IsUnique();
    }
}
