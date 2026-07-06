# Milestone 7: Identity And Collaboration

## Status

Complete

## Objective

Secure workspace data and support role-based team ownership, membership, and
task assignment.

## User Stories

- As an owner, I can manage workspace membership and roles.
- As a manager, I can assign workspace work to team members.
- As a member, I can access my workspace without administering its membership.
- As an operator, I can connect the API to a standards-based identity provider.

## Technical Tasks

- Model profiles, workspaces, memberships, and roles.
- Persist collaboration aggregates with EF Core migrations.
- Add current-user and workspace repository ports.
- Configure JWT bearer authentication for hosted environments.
- Add an isolated development/test authentication scheme.
- Add secured workspace and assignment endpoints.
- Require authentication for project, task, and dashboard APIs.
- Add identity-aware workspace and assignment UI.

## Acceptance Criteria

- Unauthenticated protected requests return `401`.
- Users only list workspaces where they hold membership.
- Unknown or inaccessible workspaces return `404`.
- Only owners administer membership and roles.
- Owners and managers assign tasks to workspace members.
- Members cannot perform assignment administration.
- Production identity comes from validated bearer claims.

## Required Tests

- Domain tests for owner and membership invariants.
- SQLite mapping, migration, and seed tests.
- API tests for `401`, `403`, `404`, role access, and assignment.
- Frontend component and browser regression tests.

## Definition Of Done

- Authentication is enforced server-side.
- Authorization decisions use persisted workspace membership.
- Production JWT configuration contains no committed secrets.
- Development identity is unavailable outside development/testing.
- Security and regression suites pass.

## Expected Commits

1. `test: specify workspace membership rules`
2. `feat: model workspace identity and roles`
3. `feat: persist workspace identity and memberships`
4. `feat: add authenticated collaboration and assignment APIs`
5. `feat: add identity-aware collaboration experience`
6. `docs: complete identity and collaboration milestone`
