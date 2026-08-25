using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;
using TodoApp.Domain.Tasks.Events;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence;

/// <summary>
/// The application's single EF Core <see cref="DbContext"/>, covering
/// project/task management, personal todos, and workspace/collaboration
/// data. Also implements <see cref="IUnitOfWork"/> so application-layer
/// handlers can commit changes without depending on EF Core directly.
///
/// Beyond plain persistence, <see cref="SaveChangesAsync"/> performs two
/// pieces of interceptor-like behavior on every save:
/// 1. it stamps a fresh <c>ConcurrencyToken</c> on any added/modified
///    aggregate root that has one, for optimistic concurrency control; and
/// 2. it inspects the change tracker (and any raised domain events) to
///    automatically build and insert <see cref="TaskActivity"/> audit rows
///    for task creation, status changes, and edits to a handful of tracked
///    fields, tags, and notes.
/// </summary>
public sealed class TodoAppDbContext(
    DbContextOptions<TodoAppDbContext> options,
    ICurrentUser? currentUser = null)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Projects, the top-level container for sprints, categories, and tasks.</summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>Work items belonging to a project.</summary>
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    /// <summary>User-defined categories used to group tasks within a project.</summary>
    public DbSet<ProjectCategory> ProjectCategories => Set<ProjectCategory>();

    /// <summary>Time-boxed sprints belonging to a project.</summary>
    public DbSet<Sprint> Sprints => Set<Sprint>();

    /// <summary>Free-text tags attached to tasks.</summary>
    public DbSet<TaskTag> TaskTags => Set<TaskTag>();

    /// <summary>Freeform notes attached to tasks.</summary>
    public DbSet<TaskNote> TaskNotes => Set<TaskNote>();

    /// <summary>Auto-generated audit trail of changes made to tasks (see <see cref="SaveChangesAsync"/>).</summary>
    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();

    /// <summary>A user's personal (non-project) to-do items.</summary>
    public DbSet<PersonalTodo> PersonalTodos => Set<PersonalTodo>();

    /// <summary>Comments left on personal to-do items.</summary>
    public DbSet<PersonalTodoComment> PersonalTodoComments =>
        Set<PersonalTodoComment>();

    /// <summary>Recurring personal routines/habits tracked per day.</summary>
    public DbSet<DailyRoutine> DailyRoutines => Set<DailyRoutine>();

    /// <summary>Basic profile information for a registered user.</summary>
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    /// <summary>Login credentials (password hash, reset token) for a user; see <see cref="UserCredential"/>.</summary>
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    /// <summary>Workspaces (tenant/team boundary) that own projects.</summary>
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    /// <summary>Membership of users within a workspace, including their role.</summary>
    public DbSet<WorkspaceMembership> WorkspaceMemberships =>
        Set<WorkspaceMembership>();

    /// <summary>Pending invitations for new members to join a workspace.</summary>
    public DbSet<WorkspaceInvitation> WorkspaceInvitations =>
        Set<WorkspaceInvitation>();

    // Applies every IEntityTypeConfiguration<T> found in this assembly,
    // keeping entity configuration in dedicated configuration classes rather
    // than inline here.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TodoAppDbContext).Assembly);
    }

    // IUnitOfWork's SaveChangesAsync just forwards to the DbContext's own
    // override below, so application-layer code that only knows about
    // IUnitOfWork still benefits from the audit/concurrency-token behavior.
    async Task IUnitOfWork.SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Persists changes, first stamping fresh concurrency tokens on
    /// added/modified aggregates and building <see cref="TaskActivity"/>
    /// audit rows from both raised domain events and tracked property
    /// changes, so audit history stays complete without callers having to
    /// remember to record it explicitly.
    /// </summary>
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
        // Attribute audit entries to the current authenticated user when
        // available (e.g. an interactive HTTP request), falling back to
        // "system" for background jobs or unauthenticated contexts.
        var actor = currentUser?.IsAuthenticated == true &&
            currentUser.UserId != Guid.Empty
                ? currentUser.UserId.ToString()
                : "system";
        var auditActivities = BuildTaskAuditActivities(occurredAt, actor);

        TaskActivities.AddRange(auditActivities);

        // Domain events raised by task aggregates (e.g. a status change
        // performed through a domain method) get their own dedicated audit
        // entry, in addition to the generic property-diffing below.
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

        // Domain events are transient signals for this save only; clear them
        // once persisted so they aren't re-processed on a later save.
        foreach (var task in tasksWithEvents)
        {
            task.ClearDomainEvents();
        }

        return result;
    }

    // Scans the change tracker for added/modified tasks, tags, and notes and
    // produces the corresponding TaskActivity audit rows by diffing tracked
    // property values (as opposed to the domain-event-driven path above,
    // which only covers status changes).
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

    // Records a single audit entry for the given property if it was
    // actually modified (comparing formatted values, since some properties
    // round-trip to the same display value despite EF marking them dirty).
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

    // Formats a raw property value for display in an audit entry, handling
    // value-object types and empty/unset values specially so they render as
    // readable text rather than a type's default ToString().
    private static string FormatAuditValue<TValue>(TValue value) =>
        value switch
        {
            null => string.Empty,
            DueDate dueDate => dueDate.Value.ToString("O"),
            EffortEstimate effort => effort.Value.ToString(),
            Guid guid when guid == Guid.Empty => string.Empty,
            _ => Truncate(value.ToString() ?? string.Empty)
        };

    // Caps audit text at 200 characters so long note bodies etc. don't bloat
    // the activity log.
    private static string Truncate(string value) =>
        value.Length <= 200 ? value : string.Concat(value.AsSpan(0, 197), "...");

    // Assigns a new ConcurrencyToken to every added/modified entity of the
    // given aggregate type, providing optimistic-concurrency protection
    // (via a rowversion-like GUID column) uniformly across all provider
    // types, including SQLite which has no native rowversion support.
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
