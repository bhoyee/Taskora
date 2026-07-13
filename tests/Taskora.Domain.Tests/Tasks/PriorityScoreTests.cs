using TodoApp.Domain.Common;
using TodoApp.Domain.Tasks;

namespace TodoApp.Domain.Tests.Tasks;

public sealed class PriorityScoreTests
{
    [Fact]
    public void Calculate_WhenPlanningFactorsAreValid_ReturnsWeightedScore()
    {
        var factors = PlanningFactors.Create(
            businessValue: 5,
            urgency: 4,
            riskReduction: 3,
            effort: 2);

        var score = PriorityScore.Calculate(factors);

        Assert.Equal(15, score.BusinessValueContribution);
        Assert.Equal(8, score.UrgencyContribution);
        Assert.Equal(6, score.RiskReductionContribution);
        Assert.Equal(14.5m, score.Value);
        Assert.Equal(PriorityBand.Critical, score.Band);
    }

    [Fact]
    public void Calculate_WhenValueToEffortRatioIsLow_ReturnsLowBand()
    {
        var factors = PlanningFactors.Create(
            businessValue: 1,
            urgency: 1,
            riskReduction: 1,
            effort: 8);

        var score = PriorityScore.Calculate(factors);

        Assert.Equal(0.88m, score.Value);
        Assert.Equal(PriorityBand.Low, score.Band);
    }

    [Theory]
    [InlineData(0, 3, 3, 3, "Business value must be between 1 and 5.")]
    [InlineData(6, 3, 3, 3, "Business value must be between 1 and 5.")]
    [InlineData(3, 0, 3, 3, "Urgency must be between 1 and 5.")]
    [InlineData(3, 6, 3, 3, "Urgency must be between 1 and 5.")]
    [InlineData(3, 3, 0, 3, "Risk reduction must be between 1 and 5.")]
    [InlineData(3, 3, 6, 3, "Risk reduction must be between 1 and 5.")]
    [InlineData(3, 3, 3, 0, "Effort must be one of 1, 2, 3, 5, or 8.")]
    [InlineData(3, 3, 3, 4, "Effort must be one of 1, 2, 3, 5, or 8.")]
    [InlineData(3, 3, 3, 9, "Effort must be one of 1, 2, 3, 5, or 8.")]
    public void Create_WhenPlanningFactorIsOutsideRange_ThrowsDomainValidationException(
        int businessValue,
        int urgency,
        int riskReduction,
        int effort,
        string expectedMessage)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => PlanningFactors.Create(
                businessValue,
                urgency,
                riskReduction,
                effort));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void SetPlanningFactors_WhenFactorsAreProvided_UpdatesTaskPriority()
    {
        var task = TaskItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Resolve production risk");
        var factors = PlanningFactors.Create(
            businessValue: 4,
            urgency: 5,
            riskReduction: 5,
            effort: 3);

        task.SetPlanningFactors(factors);

        Assert.Same(factors, task.PlanningFactors);
        Assert.Equal(10.67m, task.Priority.Value);
        Assert.Equal(PriorityBand.Critical, task.Priority.Band);
    }
}
