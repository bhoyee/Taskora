# Testing Strategy

## Purpose

Testing is part of product design, not a final verification step. TDD will be
used most strongly for domain rules and application use cases, where examples
can define behaviour before implementation.

## TDD Cycle

1. Write an acceptance criterion or concrete behaviour example.
2. Add the smallest failing automated test.
3. Confirm it fails for the expected reason.
4. Implement the smallest useful change that passes.
5. Refactor names and structure while tests remain green.
6. Commit a coherent behaviour change.

Tests should describe observable behaviour rather than private implementation.

## Test Levels

### Domain Unit Tests

**Purpose:** Verify business rules quickly without I/O.

Examples:

- A new task starts in Backlog.
- A task cannot start while a dependency is incomplete.
- Completing a task records a completion time.
- Reopening a completed task clears its completion time.
- Priority input outside its permitted range is rejected.

These tests form the largest part of the suite.

### Application Unit Tests

**Purpose:** Verify use-case orchestration with controlled dependencies.

Examples:

- Creating a task stores a valid task.
- Completing an unknown task returns a not-found result.
- A command uses the injected clock rather than system time.
- Queries apply filtering and pagination correctly.

Use fakes where they make tests clearer. Mock only boundaries and observable
collaborations, not every class.

### Infrastructure Integration Tests

**Purpose:** Verify behaviour involving EF Core and the real database provider.

Examples:

- Entity mappings persist and reload complete aggregates.
- Unique constraints and relationships are enforced.
- Important filtered queries produce the expected results.
- Migrations create a usable database.

### API Integration Tests

**Purpose:** Verify the application through HTTP using an in-process test host.

Examples:

- Valid requests return expected status codes and contracts.
- Validation failures use Problem Details.
- Authentication and authorization policies are enforced.
- A complete create-to-complete workflow succeeds.

### Frontend Tests

- Component tests for forms, board interactions, and error states.
- API contract tests for client mapping.
- End-to-end tests for a small number of critical user journeys.

## Test Naming

Use names that state scenario and result:

```csharp
Start_WhenDependencyIsIncomplete_ReturnsBlockedResult()
Complete_WhenTaskIsInProgress_RecordsCompletionTime()
```

The arrange, act, and assert sections should be visually clear. A test should
normally have one reason to fail.

## Test Data

- Builders create valid defaults and expose only relevant changes.
- Fixed clocks make time-based behaviour deterministic.
- Each integration test owns or resets its data.
- Tests must not depend on execution order.

## Coverage Policy

Coverage is a diagnostic signal, not the goal. The pipeline will publish
coverage and enforce an initial threshold after the new test projects exist.
Critical domain rules require direct tests even if another test happens to
execute the same lines.

## Pipeline Quality Gates

A pull request cannot be considered complete unless:

- Restore succeeds.
- The solution builds with warnings reviewed.
- All automated tests pass.
- Test and coverage reports are published.
- No secrets or environment-specific credentials are committed.

The current suite contains 62 domain tests and 16 application tests. Azure
DevOps runs tests with cross-platform coverage collection for feature branches,
`dev`, and `main`.

## Definition Of Done For A Feature

- Acceptance criteria are satisfied.
- Domain and application behaviour is tested at the lowest useful level.
- Public API changes have integration coverage.
- Error and authorization paths are considered.
- Documentation is updated when contracts or architecture change.
- CI is green.
