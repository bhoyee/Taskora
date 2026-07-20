using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;
using TodoApp.Domain.Tasks.Events;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence;

public sealed class TodoAppDbContext(
    DbContextOptions<TodoAppDbContext> options,
    ICurrentUser? currentUser = null)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<ProjectCategory> ProjectCategories => Set<ProjectCategory>();

    public DbSet<Sprint> Sprints => Set<Sprint>();

    public DbSet<TaskTag> TaskTags => Set<TaskTag>();

    public DbSet<TaskNote> TaskNotes => Set<TaskNote>();

    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();

    public DbSet<PersonalTodo> PersonalTodos => Set<PersonalTodo>();

    public DbSet<DailyRoutine> DailyRoutines => Set<DailyRoutine>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships =>
        Set<WorkspaceMembership>();

    public DbSet<WorkspaceInvitation> WorkspaceInvitations =>
        Set<WorkspaceInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TodoAppDbContext).Assembly);
    }

    async Task IUnitOfWork.SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await SaveChangesAsync(cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        SetConcurrencyTokens<Project>();
        SetConcurrencyTokens<TaskItem>();
        SetConcurrencyTokens<PersonalTodo>();
        SetConcurrencyTokens<DailyRoutine>();
        SetConcurrencyTokens<Workspace>();

        var tasksWithEvents = ChangeTracker
            .Entries<TaskItem>()
            .Select(entry => entry.Entity)
            .Where(task => task.DomainEvents.Count > 0)
            .ToArray();
        var occurredAt = DateTimeOffset.UtcNow;
        var actor = currentUser?.IsAuthenticated == true &&
            currentUser.UserId != Guid.Empty
                ? currentUser.UserId.ToString()
                : "system";
        var auditActivities = BuildTaskAuditActivities(occurredAt, actor);

        TaskActivities.AddRange(auditActivities);

        foreach (var task in tasksWithEvents)
        {
            foreach (var domainEvent in task.DomainEvents)
            {
                if (domainEvent is TaskStatusChangedDomainEvent statusChanged)
                {
                    TaskActivities.Add(
                        TaskActivity.StatusChanged(
                            statusChanged.TaskId,
                            statusChanged.PreviousStatus.ToString(),
                            statusChanged.CurrentStatus.ToString(),
                            occurredAt,
                            actor));
                }
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var task in tasksWithEvents)
        {
            task.ClearDomainEvents();
        }

        return result;
    }

    private IReadOnlyList<TaskActivity> BuildTaskAuditActivities(
        DateTimeOffset occurredAt,
        string actor)
    {
        var activities = new List<TaskActivity>();

        foreach (var entry in ChangeTracker.Entries<TaskItem>())
        {
            if (entry.State == EntityState.Added)
            {
                activities.Add(TaskActivity.Record(
                    entry.Entity.Id,
                    "TaskCreated",
                    string.Empty,
                    entry.Entity.Title,
                    occurredAt,
                    actor));
                continue;
            }

            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            AddPropertyActivity(
                activities,
                entry,
                nameof(TaskItem.Title),
                "TaskRenamed",
                occurredAt,
                actor);
            AddPropertyActivity(
                activities,
                entry,
                nameof(TaskItem.DueDate),
                "DueDateChanged",
                occurredAt,
                actor);
            AddPropertyActivity(
                activities,
                entry,
                nameof(TaskItem.EffortEstimate),
                "EffortChanged",
                occurredAt,
                actor);
            AddPropertyActivity(
                activities,
                entry,
                nameof(TaskItem.AssignedUserId),
                "AssignmentChanged",
                occurredAt,
                actor);
            AddPropertyActivity(
                activities,
                entry,
                nameof(TaskItem.CategoryId),
                "CategoryChanged",
                occurredAt,
                actor);
        }

        foreach (var entry in ChangeTracker.Entries<TaskTag>())
        {
            if (entry.State is EntityState.Added or EntityState.Deleted)
            {
                activities.Add(TaskActivity.Record(
                    entry.Entity.TaskId,
                    entry.State == EntityState.Added ? "TagAdded" : "TagRemoved",
                    entry.State == EntityState.Added ? string.Empty : entry.Entity.Name,
                    entry.State == EntityState.Added ? entry.Entity.Name : string.Empty,
                    occurredAt,
                    actor));
            }
        }

        foreach (var entry in ChangeTracker.Entries<TaskNote>())
        {
            if (entry.State == EntityState.Added)
            {
                activities.Add(TaskActivity.Record(
                    entry.Entity.TaskId,
                    "NoteAdded",
                    string.Empty,
                    Truncate(entry.Entity.Body),
                    occurredAt,
                    actor));
            }
        }

        return activities;
    }

    private static void AddPropertyActivity(
        ICollection<TaskActivity> activities,
        EntityEntry<TaskItem> entry,
        string propertyName,
        string activityType,
        DateTimeOffset occurredAt,
        string actor)
    {
        var property = entry.Property(propertyName);
        if (!property.IsModified)
        {
            return;
        }

        var previousValue = FormatAuditValue(property.OriginalValue);
        var currentValue = FormatAuditValue(property.CurrentValue);
        if (previousValue == currentValue)
        {
            return;
        }

        activities.Add(TaskActivity.Record(
            entry.Entity.Id,
            activityType,
            previousValue,
            currentValue,
            occurredAt,
            actor));
    }

    private static string FormatAuditValue<TValue>(TValue value) =>
        value switch
        {
            null => string.Empty,
            DueDate dueDate => dueDate.Value.ToString("O"),
            EffortEstimate effort => effort.Value.ToString(),
            Guid guid when guid == Guid.Empty => string.Empty,
            _ => Truncate(value.ToString() ?? string.Empty)
        };

    private static string Truncate(string value) =>
        value.Length <= 200 ? value : string.Concat(value.AsSpan(0, 197), "...");

    private void SetConcurrencyTokens<TEntity>()
        where TEntity : class
    {
        foreach (var entry in ChangeTracker.Entries<TEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property("ConcurrencyToken").CurrentValue =
                    Guid.NewGuid();
            }
        }
    }
}
