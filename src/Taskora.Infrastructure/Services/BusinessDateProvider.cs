using Microsoft.Extensions.Options;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

public sealed class BusinessDateOptions
{
    public string TimeZoneId { get; set; } = "Europe/London";
}

public sealed class BusinessDateProvider(
    IClock clock,
    IOptions<BusinessDateOptions> options)
    : IBusinessDateProvider
{
    private readonly TimeZoneInfo _timeZone = ResolveTimeZone(
        options.Value.TimeZoneId);

    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            clock.UtcNow,
            _timeZone).DateTime);

    public string TimeZoneId => _timeZone.Id;

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
