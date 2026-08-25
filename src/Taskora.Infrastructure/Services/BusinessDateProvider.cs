using Microsoft.Extensions.Options;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

/// <summary>Configuration for the time zone used to compute the current "business date".</summary>
public sealed class BusinessDateOptions
{
    /// <summary>IANA/Windows time zone id (e.g. "Europe/London") used to compute local calendar dates.</summary>
    public string TimeZoneId { get; set; } = "Europe/London";
}

/// <summary>
/// Provides "today" as a calendar date (<see cref="DateOnly"/>) in a
/// configured business time zone, rather than the server's local time zone
/// or raw UTC. This keeps due-date and scheduling logic consistent
/// regardless of where the app is hosted.
/// </summary>
public sealed class BusinessDateProvider(
    IClock clock,
    IOptions<BusinessDateOptions> options)
    : IBusinessDateProvider
{
    private readonly TimeZoneInfo _timeZone = ResolveTimeZone(
        options.Value.TimeZoneId);

    /// <summary>The current calendar date in the configured business time zone.</summary>
    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            clock.UtcNow,
            _timeZone).DateTime);

    /// <summary>The resolved time zone id actually in effect (may fall back to UTC; see <see cref="ResolveTimeZone"/>).</summary>
    public string TimeZoneId => _timeZone.Id;

    // Resolves the configured time zone id, falling back to UTC if it is
    // missing, blank, or not recognized by the host OS/ICU data, so an
    // invalid configuration value never crashes the application.
    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var configured = string.IsNullOrWhiteSpace(timeZoneId)
            ? "Europe/London"
            : timeZoneId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configured);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
