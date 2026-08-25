using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// DTO breaking down a task's computed priority score into its contributing factors,
/// for display alongside the score itself.
/// </summary>
public sealed record PriorityExplanationDto(
    decimal Score,
    PriorityBand Band,
    int Effort,
    int BusinessValueContribution,
    int UrgencyContribution,
    int RiskReductionContribution);
