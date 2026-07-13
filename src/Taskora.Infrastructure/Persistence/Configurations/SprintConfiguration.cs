using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Projects;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprints");
        builder.HasKey(sprint => sprint.Id);
        builder.Property(sprint => sprint.Id)
            .ValueGeneratedNever();
        builder.Property(sprint => sprint.ProjectId).IsRequired();
        builder.Property(sprint => sprint.Name)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(sprint => sprint.Goal)
            .HasMaxLength(1000);
        builder.Property(sprint => sprint.StartDate).IsRequired();
        builder.Property(sprint => sprint.EndDate).IsRequired();
        builder.Property(sprint => sprint.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(sprint => sprint.ClosedAt);
        builder.HasIndex(sprint => new { sprint.ProjectId, sprint.Status });
        builder.HasIndex(sprint => new { sprint.ProjectId, sprint.StartDate });
    }
}
