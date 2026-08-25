using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

/// <summary>
/// A normalized label attached to a <see cref="TaskItem"/> for categorization/search.
/// Created and owned exclusively through the <see cref="TaskItem"/> aggregate.
/// </summary>
public sealed class TaskTag
{
    // Reserved for ORM materialization; domain code must use the parameterized constructor.
    private TaskTag()
    {
    }

    /// <summary>
    /// Creates a tag. Internal because tags are only ever created via the owning
    /// <see cref="TaskItem"/> aggregate.
    /// </summary>
    internal TaskTag(Guid taskId, string name)
    {
        if (taskId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Task identifier is required.");
        }

        TaskId = taskId;
        Name = NormalizeName(name);
    }

    public Guid TaskId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    // Trims whitespace, strips a leading '#', lowercases, and enforces a 2-40 char
    // length so tags compare and dedupe consistently regardless of how they were typed.
    internal static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Tag name is required.");
        }

        var normalized = name.Trim().TrimStart('#').ToLowerInvariant();
        if (normalized.Length is < 2 or > 40)
        {
            throw new DomainValidationException(
                "Tag names must be between 2 and 40 characters.");
        }

        return normalized;
    }
}
