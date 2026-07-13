using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Repositories;

public sealed class DueDateNotificationReadRepository(
    TodoAppDbContext context)
    : IDueDateNotificationReadRepository
{
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
