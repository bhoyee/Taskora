using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

/// <summary>
/// Value object bundling the inputs used to compute a task's <see cref="PriorityScore"/>:
/// business value, urgency, and risk reduction (each rated 1-5), plus effort.
/// </summary>
public sealed record PlanningFactors
{
    private PlanningFactors(
        int businessValue,
        int urgency,
        int riskReduction,
        EffortEstimate effortEstimate)
    {
        BusinessValue = businessValue;
        Urgency = urgency;
        RiskReduction = riskReduction;
        EffortEstimate = effortEstimate;
    }

    public int BusinessValue { get; }

    public int Urgency { get; }

    public int RiskReduction { get; }

    public EffortEstimate EffortEstimate { get; }

    public int Effort => EffortEstimate.Value;

    /// <summary>
    /// Creates <see cref="PlanningFactors"/>, enforcing that business value, urgency,
    /// and risk reduction each fall within the 1-5 rating scale and that effort is a
    /// supported <see cref="EffortEstimate"/> value.
    /// </summary>
    public static PlanningFactors Create(
        int businessValue,
        int urgency,
        int riskReduction,
        int effort)
    {
        EnsureInRange(
            businessValue,
            minimum: 1,
            maximum: 5,
            "Business value must be between 1 and 5.");
        EnsureInRange(
            urgency,
            minimum: 1,
            maximum: 5,
            "Urgency must be between 1 and 5.");
        EnsureInRange(
            riskReduction,
            minimum: 1,
            maximum: 5,
            "Risk reduction must be between 1 and 5.");
        return new PlanningFactors(
            businessValue,
            urgency,
            riskReduction,
            EffortEstimate.Create(effort));
    }

    // Shared range check used to validate each 1-5 rating input with a tailored error message.
    private static void EnsureInRange(
        int value,
        int minimum,
        int maximum,
        string message)
    {
        if (value < minimum || value > maximum)
        {
            throw new DomainValidationException(message);
        }
    }
}
