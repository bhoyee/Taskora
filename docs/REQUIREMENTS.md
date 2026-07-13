# Product Requirements

## Problem Statement

Traditional todo applications capture what must be done but provide little
help deciding what deserves attention. Taskora will help users plan and explain
priorities using value, urgency, risk, effort, deadlines, and dependencies.

## Personas

### Individual Contributor

Needs a clear view of ready, blocked, overdue, and high-value work.

### Project Manager

Needs to understand delivery risk, dependencies, workload, and project
progress.

### Workspace Owner

Needs to manage membership, permissions, and workspace settings.

## Functional Requirements

### FR-001: Manage Projects

As a user, I want to create and update projects so that related work has a
clear boundary.

**Acceptance criteria:**

- A project requires a name.
- A project can have an optional description and target date.
- Archiving a project hides it from active views without deleting its history.

### FR-002: Manage Tasks

As a user, I want to create and edit tasks so that work is clearly defined.

**Acceptance criteria:**

- A task requires a title and project.
- A task can include a description, assignee, due date, and planning inputs.
- Completed tasks retain their completion time.
- Invalid task state transitions produce a useful error.

### FR-003: Task Workflow

As a user, I want tasks to follow a controlled lifecycle so that the board
reflects actual delivery state.

**Acceptance criteria:**

- Supported states are Backlog, Ready, In Progress, Blocked, and Completed.
- A task with incomplete dependencies cannot enter In Progress.
- Completing a task records when it was completed.
- Reopening a task clears its previous completion time.

### FR-004: Priority Intelligence

As a user, I want a calculated priority score so that I can make explainable
planning decisions.

**Acceptance criteria:**

- The score considers business value, urgency, risk reduction, and effort.
- Inputs use documented ranges and reject invalid values.
- Users can see the factors that produced a score.
- Equal scores use due date and creation time as deterministic tie-breakers.

### FR-005: Dependencies

As a user, I want to link dependent tasks so that blocked work is visible.

**Acceptance criteria:**

- A task cannot depend on itself.
- Circular dependency chains are rejected.
- Incomplete dependencies mark downstream work as blocked.
- Completing a dependency recalculates whether downstream work is ready.

### FR-006: Discover Work

As a user, I want to search, filter, sort, and page tasks so that I can find
relevant work quickly.

**Acceptance criteria:**

- Tasks can be filtered by project, status, assignee, priority, and due state.
- Tasks can be sorted by calculated priority, due date, and creation date.
- Search matches task title and description.
- API list responses include pagination metadata.

### FR-007: Activity History

As a project manager, I want important changes recorded so that decisions can
be understood later.

**Acceptance criteria:**

- Status, priority-input, assignment, and due-date changes are recorded.
- Each activity contains a timestamp and responsible user.
- Activity records cannot be edited through the public API.

### FR-008: Dashboard

As a project manager, I want delivery indicators so that I can identify risk.

**Acceptance criteria:**

- The dashboard shows ready, active, blocked, completed, and overdue counts.
- It highlights high-priority blocked work.
- It reports progress by project.

### FR-009: Identity And Authorization

As a workspace owner, I want role-based access so that workspace data is
protected.

**Acceptance criteria:**

- Authenticated users only see workspaces they belong to.
- Owners manage membership and roles.
- Managers manage projects and assignments.
- Members manage tasks where workspace policy permits.

## Non-Functional Requirements

### NFR-001: Quality

- Business rules have automated unit tests.
- Core API workflows have integration tests.
- Pull requests must pass build and test checks.

### NFR-002: Performance

- List endpoints use server-side filtering and pagination.
- Normal API requests should complete within 500 ms in the development
  performance baseline, excluding startup and external identity calls.

### NFR-003: Security

- Secrets are never committed to source control.
- Authorization is enforced server-side.
- Validation and error responses do not expose stack traces in production.

### NFR-004: Observability

- Requests use structured logs and correlation identifiers.
- The service exposes health checks.
- Production failures can be diagnosed without reproducing them locally.

### NFR-005: Maintainability

- Domain, application, infrastructure, API, and web responsibilities remain
  separated.
- Architecture decisions with long-term impact are recorded.
- Public API contracts are documented through OpenAPI.

## Out Of Scope For The First Release

- Microservices.
- Real-time chat.
- Billing and subscriptions.
- Native mobile applications.
- Machine-learning-based recommendations.
- Third-party marketplace integrations.

These may be evaluated only after the core product is deployed and measured.
