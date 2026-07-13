using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

public sealed record EffortEstimate
{
    private static readonly int[] SupportedValues = [1, 2, 3, 5, 8];

    private EffortEstimate(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static EffortEstimate Create(int value)
    {
        if (!SupportedValues.Contains(value))
        {
            throw new DomainValidationException(
                "Effort must be one of 1, 2, 3, 5, or 8.");
        }

        return new EffortEstimate(value);
    }
}
