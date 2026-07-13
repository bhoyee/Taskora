using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TodoApp.Infrastructure.Persistence;

public sealed class TodoAppDbContextFactory
    : IDesignTimeDbContextFactory<TodoAppDbContext>
{
    public TodoAppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__TodoApp") ??
            "Data Source=todoapp.db";
        var options = new DbContextOptionsBuilder<TodoAppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new TodoAppDbContext(options);
    }
}
