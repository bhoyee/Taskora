namespace TodoApp.Domain.Tasks;

/// <summary>
/// Value object representing a task's computed priority: a weighted combination of
/// business value, urgency, and risk reduction, normalized by effort (a WSJF-style
/// score), together with the <see cref="PriorityBand"/> it falls into.
/// </summary>
public sealed record PriorityScore
{
    private const int BusinessValueWeight = 3;
    private const int UrgencyWeight = 2;
    private const int RiskReductionWeight = 2;

    private PriorityScore(
        int businessValueContribution,
        int urgencyContribution,
        int riskReductionContribution,
        decimal value,
        PriorityBand band)
    {
        BusinessValueContribution = businessValueContribution;
        UrgencyContribution = urgencyContribution;
        RiskReductionContribution = riskReductionContribution;
        Value = value;
        Band = band;
    }

    public int BusinessValueContribution { get; }

    public int UrgencyContribution { get; }

    public int RiskReductionContribution { get; }

    public decimal Value { get; }

    public PriorityBand Band { get; }

    /// <summary>
    /// Computes the priority score from the given <see cref="PlanningFactors"/>: each
    /// factor is weighted, summed, and divided by effort — higher value/urgency/risk
    /// reduction and lower effort both push the score (and therefore the band) higher.
    /// </summary>
    public static PriorityScore Calculate(PlanningFactors factors)
    {
        var businessValueContribution =
            factors.BusinessValue * BusinessValueWeight;
        var urgencyContribution = factors.Urgency * UrgencyWeight;
        var riskReductionContribution =
            factors.RiskReduction * RiskReductionWeight;
        var totalContribution =
            businessValueContribution +
            urgencyContribution +
            riskReductionContribution;
        var value = Math.Round(
            (decimal)totalContribution / factors.Effort,
            decimals: 2,
            MidpointRounding.AwayFromZero);

        return new PriorityScore(
            businessValueContribution,
            urgencyContribution,
            riskReductionContribution,
            value,
            DetermineBand(value));
    }

    // Maps the numeric score to a band using fixed thresholds.
    private static PriorityBand DetermineBand(decimal value) =>
        value switch
        {
            >= 10m => PriorityBand.Critical,
            >= 6m => PriorityBand.High,
            >= 3m => PriorityBand.Medium,
            _ => PriorityBand.Low
        };
}
