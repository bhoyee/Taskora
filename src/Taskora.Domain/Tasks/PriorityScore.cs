namespace TodoApp.Domain.Tasks;

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

    private static PriorityBand DetermineBand(decimal value) =>
        value switch
        {
            >= 10m => PriorityBand.Critical,
            >= 6m => PriorityBand.High,
            >= 3m => PriorityBand.Medium,
            _ => PriorityBand.Low
        };
}
