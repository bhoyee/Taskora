using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;
using TodoApp.Domain.Tasks.Events;

namespace TodoApp.Infrastructure.Persistence;

public sealed class TodoAppDbContext(
    DbContextOptions<TodoAppDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<ProjectCategory> ProjectCategories => Set<ProjectCategory>();

    public DbSet<TaskTag> TaskTags => Set<TaskTag>();

    public DbSet<TaskNote> TaskNotes => Set<TaskNote>();

    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships =>
        Set<WorkspaceMembership>();

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
        SetConcurrencyTokens<Workspace>();

        var tasksWithEvents = ChangeTracker
            .Entries<TaskItem>()
            .Select(entry => entry.Entity)
            .Where(task => task.DomainEvents.Count > 0)
            .ToArray();

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
                            DateTimeOffset.UtcNow));
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
