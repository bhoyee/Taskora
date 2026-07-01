# Milestone 1: Domain Foundation

## Status

In progress

## Objective

Create a behaviour-rich domain model for task planning and delivery. Business
rules must be expressed through OOP methods and value objects, developed with
TDD, and remain independent of HTTP, databases, and UI frameworks.

## User Stories

### M01-US01: Control Task Lifecycle

As a contributor, I want tasks to follow valid workflow transitions so that the
board accurately represents delivery state.

### M01-US02: Model Dependencies

As a contributor, I want tasks to depend on other tasks so that blocked work is
identified before it is started.

### M01-US03: Explain Priority

As a project manager, I want priority calculated from visible planning factors
so that ordering decisions can be explained.

### M01-US04: Organise Work In Projects

As a project manager, I want tasks grouped into projects so that related work
has a lifecycle and delivery target.

## Technical Tasks

- Maintain `TodoApp.Domain` with no framework or infrastructure dependencies.
- Implement guarded `TaskItem` lifecycle operations.
- Implement dependency add/remove operations and cycle detection.
- Implement immutable planning factors and priority score value objects.
- Implement `Project` creation, editing, and archive behaviour.
- Add title, due-date, and effort value objects where rules justify them.
- Introduce domain events for significant lifecycle changes.
- Keep all entity state mutation private.

## Acceptance Criteria

- New tasks start in Backlog.
- Only Ready tasks without incomplete dependencies can start.
- Only In Progress tasks can complete.
- Blocking records a reason; unblocking returns work to Ready.
- Reopening clears the previous completion timestamp.
- Self, duplicate, and circular dependencies are rejected.
- Priority inputs enforce documented ranges.
- Priority output exposes weighted contributions and a priority band.
- Projects require valid identity and name.
- Archived projects cannot accept new tasks.
- Domain objects do not reference ASP.NET Core or EF Core.

## Required Tests

- Unit tests for every supported and rejected task transition.
- Unit tests for blank and invalid values.
- Unit tests for direct and transitive dependency cycles.
- Unit tests for effective blocked-state changes.
- Unit tests for score calculation, rounding, ranges, and priority bands.
- Unit tests for project creation, updates, and archive restrictions.
- Unit tests for emitted domain events.

## Definition Of Done

- All acceptance criteria have automated tests.
- Domain tests pass locally and in Azure DevOps.
- The solution builds with zero warnings.
- No domain state can bypass business methods through public setters.
- Milestone documentation reflects delivered and deferred scope.
- A pull request from `feature/domain-foundation` to `dev` passes review.

## Expected Commits

1. `build: establish domain test solution structure`
2. `test: specify task lifecycle domain behaviour`
3. `feat: implement guarded task lifecycle`
4. `test: specify task dependency rules`
5. `feat: enforce task dependency constraints`
6. `test: specify explainable priority scoring`
7. `feat: implement explainable priority scoring`
8. `test: specify project domain behaviour`
9. `feat: implement project aggregate rules`
10. `test: specify domain event behaviour`
11. `feat: record task lifecycle domain events`
12. `docs: complete domain foundation milestone`

## Current Progress

Delivered:

- Task lifecycle and validation.
- Dependency management and cycle detection.
- Effective blocked-state calculation.
- Explainable priority scoring.
- 30 passing domain tests.

Remaining:

- Project aggregate.
- Due-date and effort value objects.
- Domain events.
- Final milestone review and pull request.
