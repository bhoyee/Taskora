using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Domain.Projects;

public sealed class Project
{
    private readonly List<ProjectCategory> _categories = [];

    private Project(
        Guid id,
        string name,
        string? description,
        Guid workspaceId)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "Project identifier is required.");
        }

        Id = id;
        WorkspaceId = workspaceId;
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string? Description { get; private set; }

    public bool IsArchived => ArchivedAt.HasValue;

    public DateTimeOffset? ArchivedAt { get; private set; }

    public DueDate? TargetDate { get; private set; }

    public IReadOnlyCollection<ProjectCategory> Categories =>
        _categories.AsReadOnly();

    public static Project Create(
        Guid id,
        string name,
        string? description = null,
        Guid? workspaceId = null) =>
        new(id, name, description, workspaceId ?? Guid.Empty);

    public void Rename(string name)
    {
        EnsureActive();
        Name = NormalizeName(name);
    }

    public void UpdateDescription(string? description)
    {
        EnsureActive();
        Description = NormalizeDescription(description);
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        EnsureActive();
        ArchivedAt = archivedAt;
    }

    public void SetTargetDate(DueDate targetDate)
    {
        EnsureActive();
        TargetDate = targetDate;
    }

    public ProjectCategory AddCategory(Guid id, string name)
    {
        EnsureActive();
        var category = new ProjectCategory(id, Id, name);
        if (_categories.Any(existing =>
                existing.Name.Equals(
                    category.Name,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainRuleException(
                "The project category already exists.");
        }

        _categories.Add(category);
        return category;
    }

    public void RenameCategory(Guid categoryId, string name)
    {
        EnsureActive();
        GetCategory(categoryId).Rename(name);
    }

    public bool HasCategory(Guid categoryId) =>
        _categories.Any(category => category.Id == categoryId);

    private ProjectCategory GetCategory(Guid categoryId) =>
        _categories.SingleOrDefault(category => category.Id == categoryId) ??
        throw new DomainRuleException("The project category was not found.");

    public void EnsureCanAcceptTasks()
    {
        if (IsArchived)
        {
            throw new DomainRuleException(
                "Archived projects cannot accept new tasks.");
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Project name is required.");
        }

        return name.Trim();
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private void EnsureActive()
    {
        if (IsArchived)
        {
            throw new DomainRuleException(
                "Archived projects cannot be changed.");
        }
    }
}
