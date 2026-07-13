using TodoApp.Application.Tasks.CreateTask;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tests.Architecture;

public sealed class DependencyRuleTests
{
    [Fact]
    public void Application_DoesNotReferenceDeliveryOrPersistenceFrameworks()
    {
        var references = typeof(CreateTaskHandler)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain(
            references,
            name => name?.StartsWith("Microsoft.AspNetCore") == true);
        Assert.DoesNotContain(
            references,
            name => name?.StartsWith("Microsoft.EntityFrameworkCore") == true);
    }

    [Fact]
    public void Domain_DoesNotReferenceApplicationOrFrameworks()
    {
        var references = typeof(TaskItem)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("TodoApp.Application", references);
        Assert.DoesNotContain(
            references,
            name => name?.StartsWith("Microsoft.AspNetCore") == true);
        Assert.DoesNotContain(
            references,
            name => name?.StartsWith("Microsoft.EntityFrameworkCore") == true);
    }
}
