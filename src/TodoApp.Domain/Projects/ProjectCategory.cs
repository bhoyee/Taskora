using TodoApp.Domain.Common;

namespace TodoApp.Domain.Projects;

public sealed class ProjectCategory
{
    private ProjectCategory()
    {
    }

    internal ProjectCategory(Guid id, Guid projectId, string name)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "Category identifier is required.");
        }

        Id = id;
        ProjectId = projectId;
        Name = NormalizeName(name);
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    internal void Rename(string name) => Name = NormalizeName(name);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Category name is required.");
        }

        return name.Trim();
    }
}
