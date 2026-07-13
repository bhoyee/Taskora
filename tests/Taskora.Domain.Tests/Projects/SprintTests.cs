using TodoApp.Domain.Common;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Domain.Tests.Projects;

public sealed class SprintTests
{
    private static readonly Guid ProjectId =
        Guid.Parse("48b93191-7a32-4e87-8721-8ba65cfe4280");
    private static readonly Guid SprintId =
        Guid.Parse("316eb9c4-a786-4db3-b2b9-51c9db4de0a6");

    [Fact]
    public void Create_WhenDetailsAreValid_CreatesPlannedSprint()
    {
        var sprint = CreateSprint();

        Assert.Equal(SprintId, sprint.Id);
        Assert.Equal(ProjectId, sprint.ProjectId);
        Assert.Equal("Portfolio hardening", sprint.Name);
        Assert.Equal(SprintStatus.Planned, sprint.Status);
        Assert.Null(sprint.ClosedAt);
    }

    [Fact]
    public void Create_WhenEndDateIsBeforeStartDate_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Sprint.Create(
                SprintId,
                ProjectId,
                "Invalid sprint",
                null,
                new DateOnly(2026, 7, 20),
                new DateOnly(2026, 7, 12)));

        Assert.Equal(
            "Sprint end date cannot be before the start date.",
            exception.Message);
    }

    [Fact]
    public void Start_WhenSprintIsPlanned_MarksSprintActive()
    {
        var sprint = CreateSprint();

        sprint.Start();

        Assert.Equal(SprintStatus.Active, sprint.Status);
    }

    [Fact]
    public void Complete_WhenSprintIsActive_ClosesSprint()
    {
        var sprint = CreateSprint();
        sprint.Start();
        var closedAt = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

        sprint.Complete(closedAt);

        Assert.Equal(SprintStatus.Completed, sprint.Status);
        Assert.Equal(closedAt, sprint.ClosedAt);
    }

    [Fact]
    public void Update_WhenSprintIsActive_ThrowsDomainRuleException()
    {
        var sprint = CreateSprint();
        sprint.Start();

        var exception = Assert.Throws<DomainRuleException>(() =>
            sprint.Update(
                "New name",
                "New goal",
                new DateOnly(2026, 7, 15),
                new DateOnly(2026, 7, 26)));

        Assert.Equal("Only planned sprints can be edited.", exception.Message);
    }

    [Fact]
    public void Project_AddSprint_WhenNameAlreadyExists_ThrowsDomainRuleException()
    {
        var project = Project.Create(ProjectId, "Portfolio launch");
        project.AddSprint(
            SprintId,
            "Portfolio hardening",
            null,
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 24));

        var exception = Assert.Throws<DomainRuleException>(() =>
            project.AddSprint(
                Guid.NewGuid(),
                " portfolio hardening ",
                null,
                new DateOnly(2026, 7, 27),
                new DateOnly(2026, 8, 7)));

        Assert.Equal(
            "An active sprint with this name already exists.",
            exception.Message);
    }

    [Fact]
    public void TaskItem_AssignSprint_WhenIdentifierIsValid_RecordsSprint()
    {
        var task = TaskItem.Create(
            Guid.Parse("89b7a01b-c6d4-466b-a5a7-b568e810c486"),
            ProjectId,
            "Create sprint UI");

        task.AssignSprint(SprintId);

        Assert.Equal(SprintId, task.SprintId);
    }

    private static Sprint CreateSprint() =>
        Sprint.Create(
            SprintId,
            ProjectId,
            "  Portfolio hardening  ",
            "  Stabilise delivery readiness.  ",
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 24));
}
