using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Domain.Projects;

/// <summary>
/// Aggregate root for a project: owns its categories and sprints, and once archived
/// becomes immutable and stops accepting new tasks, categories, or sprints.
/// </summary>
public sealed class Project
{
    private readonly List<ProjectCategory> _categories = [];
    private readonly List<Sprint> _sprints = [];

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

    public IReadOnlyCollection<Sprint> Sprints =>
        _sprints.AsReadOnly();

    /// <summary>Creates a new, active (unarchived) project.</summary>
    public static Project Create(
        Guid id,
        string name,
        string? description = null,
        Guid? workspaceId = null) =>
        new(id, name, description, workspaceId ?? Guid.Empty);

    /// <summary>Renames the project. Not permitted once archived.</summary>
    public void Rename(string name)
    {
        EnsureActive();
        Name = NormalizeName(name);
    }

    /// <summary>Updates the project description. Not permitted once archived.</summary>
    public void UpdateDescription(string? description)
    {
        EnsureActive();
        Description = NormalizeDescription(description);
    }

    /// <summary>Archives the project, after which it becomes read-only and stops accepting new tasks.</summary>
    public void Archive(DateTimeOffset archivedAt)
    {
        EnsureActive();
        ArchivedAt = archivedAt;
    }

    /// <summary>Sets the project's target completion date. Not permitted once archived.</summary>
    public void SetTargetDate(DueDate targetDate)
    {
        EnsureActive();
        TargetDate = targetDate;
    }

    /// <summary>
    /// Adds a new category to the project, rejecting a duplicate name
    /// (case-insensitive) among existing categories.
    /// </summary>
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

    /// <summary>Renames an existing category. Not permitted once the project is archived.</summary>
    public void RenameCategory(Guid categoryId, string name)
    {
        EnsureActive();
        GetCategory(categoryId).Rename(name);
    }

    public bool HasCategory(Guid categoryId) =>
        _categories.Any(category => category.Id == categoryId);

    /// <summary>
    /// Adds a new sprint to the project, rejecting a duplicate name (case-insensitive)
    /// among sprints that are not cancelled (a cancelled sprint's name can be reused).
    /// </summary>
    public Sprint AddSprint(
        Guid id,
        string name,
        string? goal,
        DateOnly startDate,
        DateOnly endDate)
    {
        EnsureActive();
        var sprint = Sprint.Create(id, Id, name, goal, startDate, endDate);
        if (_sprints.Any(existing =>
                existing.Name.Equals(
                    sprint.Name,
                    StringComparison.OrdinalIgnoreCase) &&
                existing.Status != SprintStatus.Cancelled))
        {
            throw new DomainRuleException(
                "An active sprint with this name already exists.");
        }

        _sprints.Add(sprint);
        return sprint;
    }

    /// <summary>Looks up a sprint belonging to this project by id, throwing if not found.</summary>
    public Sprint GetSprint(Guid sprintId) =>
        _sprints.SingleOrDefault(sprint => sprint.Id == sprintId) ??
        throw new DomainRuleException("The sprint was not found.");

    /// <summary>Removes a sprint from the project.</summary>
    public void RemoveSprint(Guid sprintId)
    {
        var sprint = GetSprint(sprintId);
        _sprints.Remove(sprint);
    }

    public bool HasSprint(Guid sprintId) =>
        _sprints.Any(sprint => sprint.Id == sprintId);

    // Looks up a category belonging to this project by id, throwing if not found.
    private ProjectCategory GetCategory(Guid categoryId) =>
        _categories.SingleOrDefault(category => category.Id == categoryId) ??
        throw new DomainRuleException("The project category was not found.");

    /// <summary>
    /// Guard used by other aggregates (e.g. when creating a task under this project)
    /// to enforce that archived projects cannot receive new work.
    /// </summary>
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

    // Central guard: most mutations are disallowed once the project is archived.
    private void EnsureActive()
    {
        if (IsArchived)
        {
            throw new DomainRuleException(
                "Archived projects cannot be changed.");
        }
    }
}
