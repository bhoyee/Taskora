using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Configurations;

internal sealed class PersonalTodoCommentConfiguration
    : IEntityTypeConfiguration<PersonalTodoComment>
{
    public void Configure(EntityTypeBuilder<PersonalTodoComment> builder)
    {
        builder.ToTable("PersonalTodoComments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id)
            .ValueGeneratedNever();
        builder.Property(comment => comment.TodoId).IsRequired();
        builder.Property(comment => comment.Body)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(comment => comment.CreatedAt)
            .HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        builder.HasIndex(comment => new { comment.TodoId, comment.CreatedAt });
    }
}
