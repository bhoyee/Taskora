using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

/// <summary>
/// Value object representing relative task effort, constrained to a Fibonacci-like
/// scale (1, 2, 3, 5, 8) as commonly used in agile estimation.
/// </summary>
public sealed record EffortEstimate
{
    private static readonly int[] SupportedValues = [1, 2, 3, 5, 8];

    private EffortEstimate(int value)
    {
        Value = value;
    }

    public int Value { get; }

    /// <summary>Creates an <see cref="EffortEstimate"/>, rejecting any value outside the supported scale.</summary>
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
