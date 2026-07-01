using TodoApp.Domain.Common;

namespace TodoApp.Domain.Tasks;

public sealed record PlanningFactors
{
    private PlanningFactors(
        int businessValue,
        int urgency,
        int riskReduction,
        int effort)
    {
        BusinessValue = businessValue;
        Urgency = urgency;
        RiskReduction = riskReduction;
        Effort = effort;
    }

    public int BusinessValue { get; }

    public int Urgency { get; }

    public int RiskReduction { get; }

    public int Effort { get; }

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
        EnsureInRange(
            effort,
            minimum: 1,
            maximum: 8,
            "Effort must be between 1 and 8.");

        return new PlanningFactors(
            businessValue,
            urgency,
            riskReduction,
            effort);
    }

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
