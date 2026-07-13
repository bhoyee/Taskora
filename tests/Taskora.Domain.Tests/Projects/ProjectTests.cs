using TodoApp.Domain.Common;
using TodoApp.Domain.Projects;

namespace TodoApp.Domain.Tests.Projects;

public sealed class ProjectTests
{
    private static readonly Guid ProjectId =
        Guid.Parse("beebed9e-72e3-497f-8d2b-c253ee0bdcd8");

    [Fact]
    public void Create_WhenDetailsAreValid_CreatesActiveProject()
    {
        var project = Project.Create(
            ProjectId,
            "  Portfolio launch  ",
            "  Deliver the public portfolio application.  ");

        Assert.Equal(ProjectId, project.Id);
        Assert.Equal("Portfolio launch", project.Name);
        Assert.Equal(
            "Deliver the public portfolio application.",
            project.Description);
        Assert.False(project.IsArchived);
        Assert.Null(project.ArchivedAt);
    }

    [Fact]
    public void Create_WhenIdentifierIsEmpty_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => Project.Create(Guid.Empty, "Portfolio launch"));

        Assert.Equal("Project identifier is required.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameIsBlank_ThrowsDomainValidationException(string name)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => Project.Create(ProjectId, name));

        Assert.Equal("Project name is required.", exception.Message);
    }

    [Fact]
    public void Rename_WhenProjectIsActive_UpdatesTrimmedName()
    {
        var project = CreateProject();

        project.Rename("  Production launch  ");

        Assert.Equal("Production launch", project.Name);
    }

    [Fact]
    public void UpdateDescription_WhenProjectIsActive_UpdatesDescription()
    {
        var project = CreateProject();

        project.UpdateDescription("  Updated delivery scope.  ");

        Assert.Equal("Updated delivery scope.", project.Description);
    }

    [Fact]
    public void Archive_WhenProjectIsActive_RecordsArchiveTime()
    {
        var archivedAt =
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var project = CreateProject();

        project.Archive(archivedAt);

        Assert.True(project.IsArchived);
        Assert.Equal(archivedAt, project.ArchivedAt);
    }

    [Fact]
    public void Rename_WhenProjectIsArchived_ThrowsDomainRuleException()
    {
        var project = CreateProject();
        project.Archive(DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainRuleException>(
            () => project.Rename("New name"));

        Assert.Equal("Archived projects cannot be changed.", exception.Message);
    }

    [Fact]
    public void EnsureCanAcceptTasks_WhenProjectIsArchived_ThrowsDomainRuleException()
    {
        var project = CreateProject();
        project.Archive(DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainRuleException>(
            project.EnsureCanAcceptTasks);

        Assert.Equal(
            "Archived projects cannot accept new tasks.",
            exception.Message);
    }

    private static Project CreateProject() =>
        Project.Create(ProjectId, "Portfolio launch");
}
