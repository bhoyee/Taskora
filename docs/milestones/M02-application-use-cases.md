# Milestone 2: Application Use Cases

## Status

In progress

## Objective

Expose domain behaviour through testable commands and queries while keeping
delivery, persistence, identity, and time behind application-owned interfaces.

## User Stories

### M02-US01: Execute Task Actions

As an API client, I want explicit task operations so that each request has a
clear purpose and predictable result.

### M02-US02: Find Relevant Work

As a contributor, I want filtered, sorted, and paginated task queries so that I
can focus on relevant work.

### M02-US03: View Project Delivery

As a project manager, I want project-board and summary queries so that I can
understand progress and blockers.

## Technical Tasks

- Create `TodoApp.Application` and `TodoApp.Application.Tests`.
- Reference Domain from Application without referencing Infrastructure or API.
- Add commands for create, edit, move, block, complete, and reopen.
- Add commands for dependency and planning-factor changes.
- Add project commands.
- Add task-detail, task-list, and project-board queries.
- Define repository, unit-of-work, identity, and clock interfaces.
- Add DTO mapping, validation results, pagination, and cancellation.
- Define consistent application result and error types.

## Acceptance Criteria

- Each command invokes domain behaviour rather than changing state directly.
- Unknown identifiers return a typed not-found result.
- Domain rule failures are translated without losing their meaning.
- Queries support project, status, blocked state, priority, and due-state filters.
- List results contain pagination metadata.
- Time-sensitive operations use an injected clock.
- Application code has no ASP.NET Core, EF Core, or database-provider references.

## Required Tests

- Command success tests.
- Command not-found, validation, and domain-rule failure tests.
- Query filtering, sorting, and pagination tests.
- Cancellation propagation tests for asynchronous operations.
- Tests proving the injected clock supplies timestamps.
- Architecture tests for forbidden project references.

## Definition Of Done

- Every use case has success and important failure-path coverage.
- All application tests run in CI.
- Public application contracts are documented.
- No application handler depends on an HTTP or EF Core type.
- A pull request from the feature branch to `dev` passes review.

## Expected Commits

1. `build: add application and application test projects`
2. `test: specify create task use case`
3. `feat: implement create task command`
4. `test: specify task lifecycle commands`
5. `feat: implement task lifecycle commands`
6. `test: specify task list query behaviour`
7. `feat: implement filtered paginated task queries`
8. `test: enforce application dependency rules`
9. `docs: complete application use case milestone`

## Current Progress

Delivered:

- Application and test project boundaries.
- Typed `Result<T>`, application errors, and pagination results.
- Repository, unit-of-work, identifier, and clock interfaces.
- CreateTask with project validation, scheduling, and effort.
- StartTask and CompleteTask lifecycle commands.
- AddTaskDependency with typed failure results.
- GetTaskById details query.
- Filtered, sorted, paginated task search contract.
- 16 passing application tests.

Remaining:

- Edit, move-to-ready, block, unblock, reopen, and planning-factor commands.
- Project commands and project-board summaries.
- Application architecture tests.
- Final milestone review and pull request after Milestone 1 merges.
