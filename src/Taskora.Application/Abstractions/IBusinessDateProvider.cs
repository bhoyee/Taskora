namespace TodoApp.Application.Abstractions;

/// <summary>
/// Provides the current "business date" used for due-date and scheduling logic,
/// evaluated in the application's configured business time zone rather than raw UTC.
/// </summary>
public interface IBusinessDateProvider
{
    /// <summary>Gets the current date in the application's configured business time zone.</summary>
    DateOnly Today { get; }

    /// <summary>Gets the identifier of the time zone used to compute <see cref="Today"/>.</summary>
    string TimeZoneId { get; }
}
