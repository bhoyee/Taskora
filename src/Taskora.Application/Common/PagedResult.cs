namespace TodoApp.Application.Common;

/// <summary>
/// A paging envelope wrapping a single page of <typeparamref name="T"/> items
/// together with the total item count and the paging parameters used to
/// produce the page, so callers can render pagination controls without a
/// separate count query.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    /// <summary>
    /// The total number of pages available for <see cref="TotalCount"/> items
    /// at the current <see cref="PageSize"/>, or zero when there are no items.
    /// </summary>
    public int TotalPages =>
        TotalCount == 0
            ? 0
            : (int)Math.Ceiling((decimal)TotalCount / PageSize);
}
