namespace TodoApp.Api.Contracts;

public sealed record CreatePersonalTodoRequest(
    string Title,
    DateOnly? TodoDate,
    string? Notes);

public sealed record UpdatePersonalTodoRequest(
    string Title,
    DateOnly? TodoDate,
    string? Notes);
