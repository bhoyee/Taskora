namespace TodoApp.Application.Abstractions;

/// <summary>
/// Abstraction over a unit of work, used to commit all pending changes made
/// across repositories within the current operation as a single transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes tracked by the current unit of work.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
