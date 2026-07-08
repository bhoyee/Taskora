# Milestone 9: Task Metadata And Account Access

## Status

Complete

## Objective

Complete the portfolio feature checklist by adding task categories, tags, notes,
and self-service account access.

## User Stories

- As a user, I can register and log in without relying on a development header.
- As a user, I can organize work with project-owned categories.
- As a user, I can label tasks with tags for flexible filtering.
- As a user, I can add notes/comments to tasks for collaboration context.
- As a reviewer, I can see these behaviours covered by automated tests.

## Technical Tasks

- Add category, tag, and note domain behaviour.
- Persist categories, task tags, and task notes through EF Core.
- Add application commands and queries for metadata operations.
- Add account registration and login endpoints with password hashing.
- Issue development-friendly bearer tokens for registered users.
- Add API contracts and integration tests.
- Add React UI for categories, tags, notes, login, and registration.

## Acceptance Criteria

- Tasks can be assigned to project categories.
- Tasks can have tags added and removed without duplicates.
- Tasks can have actor-attributed notes.
- Task search can filter by category and tag.
- A new user can register, log in, and receive a usable access token.
- Invalid account and metadata requests return consistent Problem Details.

## Required Tests

- Domain tests for category, tag, and note rules.
- Application tests for metadata use cases.
- Persistence tests for mappings and filters.
- API integration tests for account access and metadata endpoints.
- Frontend tests for login/register and metadata editing.

## Definition Of Done

- Feature checklist items are fully implemented, not placeholders.
- New data survives database round-trips.
- UI exposes the new capabilities.
- Tests pass locally and in CI.
- Branch is pushed and reviewed through a pull request.

## Expected Commits

1. `docs: define task metadata and account access milestone`
2. `feat: model task categories tags and notes`
3. `feat: persist task metadata and account credentials`
4. `feat: add task metadata and account APIs`
5. `feat: add metadata and account access UI`
6. `docs: complete task metadata and account access milestone`
