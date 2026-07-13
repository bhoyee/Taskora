# Milestone 3: Persistence

## Status

Complete

## Objective

Persist domain state reliably with Entity Framework Core while keeping database
technology outside the Domain and Application projects.

## User Stories

### M03-US01: Preserve Work

As a contributor, I want tasks and projects to survive application restarts so
that delivery history is not lost.

### M03-US02: Recreate Environments

As an engineer, I want versioned migrations and seed data so that development,
test, and hosted databases can be created consistently.

### M03-US03: Query Efficiently

As a user, I want task lists to remain responsive as data grows.

## Technical Tasks

- Create `Taskora.Infrastructure` and integration-test projects.
- Add EF Core with SQLite for local development.
- Implement `TodoAppDbContext` and explicit entity configurations.
- Map entities, value objects, dependencies, and domain events safely.
- Implement application repository and unit-of-work interfaces.
- Add initial migration and repeatable development seed data.
- Add Azure SQL configuration without storing secrets in source control.
- Add indexes for primary filtering and sorting paths.
- Define transaction and concurrency behaviour.

## Acceptance Criteria

- Projects, tasks, planning factors, dependencies, and history persist correctly.
- Data reloads without bypassing domain invariants.
- A clean migration creates a usable database.
- Applying migrations twice is safe.
- Development seed data is never inserted in production automatically.
- Circular dependency protection remains enforced through application behaviour.
- Queries use server-side filtering and pagination.
- Connection strings come from configuration providers.

## Required Tests

- Integration tests for each entity and value-object mapping.
- Round-trip persistence tests for complete aggregates.
- Relationship and delete-behaviour tests.
- Migration test against a clean database.
- Repository filtering, sorting, and pagination tests.
- Transaction rollback and optimistic-concurrency tests.

## Definition Of Done

- Data survives API restart.
- Migrations create and upgrade the local database.
- Integration tests use isolated databases and pass in CI.
- No infrastructure types leak into Domain or Application.
- Azure SQL configuration is documented and secret-free.
- A pull request to `dev` passes review.

## Expected Commits

1. `build: add infrastructure integration test projects`
2. `test: specify task persistence mappings`
3. `feat: add EF Core context and task mappings`
4. `test: specify project and dependency persistence`
5. `feat: persist project and dependency aggregates`
6. `feat: add initial database migration and seed data`
7. `test: verify repository queries and transactions`
8. `feat: configure SQLite and Azure SQL providers`
9. `docs: complete persistence milestone`

## Current Progress

Delivered:

- `Taskora.Infrastructure` and integration-test projects.
- EF Core 10.0.9 with SQLite and SQL Server providers.
- Secured SQLite native bundle without known vulnerable packages.
- Project, task, planning, priority, dependency, and activity mappings.
- Repository implementations for commands, task search, and project boards.
- Server-side filters, ordering, pagination, and dashboard aggregation.
- Restricted deletes, transaction rollback, and concurrency tokens.
- Initial SQLite migration and local EF tool manifest.
- Idempotent development seed data.
- File-backed restart and provider-selection tests.
- 16 passing Infrastructure integration tests.

Deferred to later milestones:

- API startup migration and development-seed invocation.
- Azure SQL deployment migrations and smoke tests.
- Production secret provisioning through Azure configuration.
