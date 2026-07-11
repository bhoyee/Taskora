using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

public sealed class TaskTag
{
    private TaskTag()
    {
    }

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
