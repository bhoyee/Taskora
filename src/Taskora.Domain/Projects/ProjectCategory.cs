using TodoApp.Domain.Common;

namespace TodoApp.Domain.Projects;

/// <summary>
/// A named grouping used to categorize work items within a <see cref="Project"/>.
/// Created and mutated only through the owning <see cref="Project"/> aggregate.
/// </summary>
public sealed class ProjectCategory
{
    // Reserved for ORM materialization; domain code must use the parameterized constructor.
    private ProjectCategory()
    {
    }

    /// <summary>
    /// Creates a category. Internal because categories are only ever created by the
    /// <see cref="Project"/> aggregate root, which owns the collection.
    /// </summary>
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

    // Renames the category, applying the same normalization/validation as construction.
    internal void Rename(string name) => Name = NormalizeName(name);

    // Trims the name and rejects blank values, keeping category names consistent.
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Category name is required.");
        }

        return name.Trim();
    }
}
