namespace TodoApp.Application.Abstractions;

public interface IBusinessDateProvider
{
    DateOnly Today { get; }

    string TimeZoneId { get; }
}
