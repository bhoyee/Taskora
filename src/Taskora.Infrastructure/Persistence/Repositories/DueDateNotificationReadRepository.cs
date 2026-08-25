using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository that computes due-date reminder notifications for
/// tasks and projects. Queries here are analytical/reporting in nature and
/// do not track entities, so results are plain snapshot values rather than
/// domain aggregates.
/// </summary>
public sealed class DueDateNotificationReadRepository(
    TodoAppDbContext context)
    : IDueDateNotificationReadRepository
{
    /// <summary>
    /// Finds incomplete tasks whose due date falls today, tomorrow, or two
    /// days out, and resolves the email recipients (creator and assignee)
    /// for each. The candidate task set and reminder-date filter are
    /// evaluated in memory after the initial <c>AsNoTracking()</c> query
    /// because <c>DueDate</c> comparisons involving <see cref="DateOnly"/>
    /// equality against an in-memory array are done client-side; only tasks
    /// with a non-null due date and a non-completed status are pulled from
    /// the database first to keep the transferred set small.
    /// </summary>
    public async Task<IReadOnlyList<TaskDueNotification>> GetTaskDueNotificationsAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var reminderDates = new[]
        {
            today,
            today.AddDays(1),
            today.AddDays(2)
        };
        var tasks = await context.Tasks
            .AsNoTracking()
            .Where(task =>
                task.DueDate != null &&
                task.Status != TaskItemStatus.Completed)
            .Select(task => new
            {
                task.Id,
                task.ProjectId,
                task.Title,
                task.DueDate,
                task.CreatedByUserId,
                task.AssignedUserId
            })
            .ToArrayAsync(cancellationToken);
        var dueTasks = tasks
            .Where(task => reminderDates.Contains(task.DueDate!.Value))
            .ToArray();
        var recipientIds = dueTasks
            .SelectMany(task => new[] { task.CreatedByUserId, task.AssignedUserId })
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToArray();
        var emails = await context.UserProfiles
            .AsNoTracking()
            .Where(user => recipientIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.Email,
                cancellationToken);

        return dueTasks
            .Select(task => new TaskDueNotification(
                task.Id,
                task.ProjectId,
                task.Title,
                task.DueDate!.Value,
                task.DueDate.Value.DayNumber - today.DayNumber,
                new[] { task.CreatedByUserId, task.AssignedUserId }
                    .Where(userId => userId.HasValue)
                    .Select(userId => userId!.Value)
                    .Distinct()
                    .Where(emails.ContainsKey)
                    .Select(userId => emails[userId])
                    .ToArray()))
            .Where(reminder => reminder.Recipients.Count > 0)
            .ToArray();
    }

    /// <summary>
    /// Finds active (non-archived) projects whose target/delivery date is
    /// exactly one day away, then resolves Owner and Manager workspace
    /// members as the notification recipients via an explicit join between
    /// <c>WorkspaceMemberships</c> and <c>UserProfiles</c> (no navigation
    /// property is used, so the join is written by hand with LINQ
    /// <c>Join</c>). Both queries use <c>AsNoTracking()</c> since results
    /// are read-only projections.
    /// </summary>
    public async Task<IReadOnlyList<ProjectTargetNotification>> GetProjectTargetNotificationsAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var targetDate = today.AddDays(1);
        var projects = await context.Projects
            .AsNoTracking()
            .Where(project =>
                project.ArchivedAt == null &&
                project.TargetDate != null)
            .Select(project => new
            {
                project.Id,
                project.Name,
                project.TargetDate,
                project.WorkspaceId
            })
            .ToArrayAsync(cancellationToken);
        var dueProjects = projects
            .Where(project => project.TargetDate!.Value == targetDate)
            .ToArray();
        var workspaceIds = dueProjects
            .Select(project => project.WorkspaceId)
            .Distinct()
            .ToArray();
        var memberRows = await context.WorkspaceMemberships
            .AsNoTracking()
            .Where(member =>
                workspaceIds.Contains(member.WorkspaceId) &&
                (member.Role == WorkspaceRole.Owner ||
                 member.Role == WorkspaceRole.Manager))
            .Join(
                context.UserProfiles.AsNoTracking(),
                member => member.UserId,
                user => user.Id,
                (member, user) => new
                {
                    member.WorkspaceId,
                    user.Email
                })
            .ToArrayAsync(cancellationToken);

        return dueProjects
            .Select(project => new ProjectTargetNotification(
                project.Id,
                project.Name,
                project.TargetDate!.Value,
                1,
                memberRows
                    .Where(member => member.WorkspaceId == project.WorkspaceId)
                    .Select(member => member.Email)
                    .Distinct()
                    .ToArray()))
            .Where(reminder => reminder.Recipients.Count > 0)
            .ToArray();
    }
}
