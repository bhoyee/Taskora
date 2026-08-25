namespace TodoApp.Domain.Tasks;

/// <summary>
/// Human-facing priority tier that a numeric <see cref="PriorityScore"/> is bucketed into.
/// </summary>
public enum PriorityBand
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
