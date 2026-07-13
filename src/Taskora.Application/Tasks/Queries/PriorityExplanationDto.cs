using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

public sealed record PriorityExplanationDto(
    decimal Score,
    PriorityBand Band,
    int Effort,
    int BusinessValueContribution,
    int UrgencyContribution,
    int RiskReductionContribution);
