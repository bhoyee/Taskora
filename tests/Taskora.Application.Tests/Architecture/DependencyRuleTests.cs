using TodoApp.Application.Tasks.CreateTask;
using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tests.Architecture;

// Architecture tests enforcing the modular-monolith layering rules: the
// Application layer must stay framework-agnostic, and the Domain layer must
// not depend on Application or on any delivery/persistence framework.
public sealed class DependencyRuleTests
{
    // Rule: Application must not reference ASP.NET Core or EF Core assemblies
    // directly (those belong to the delivery/persistence layers).
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

    // Rule: Domain must not reference the Application layer or ASP.NET
    // Core/EF Core assemblies, keeping the domain model dependency-free.
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
