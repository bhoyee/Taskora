# Product Roadmap

## Product Vision

TodoApp will become a priority-intelligence workspace that helps individuals
and teams decide what to work on, not only record tasks. It will combine task
delivery, dependencies, risk, effort, and business value in one workflow.

The application will be delivered as a modular monolith. This keeps deployment
simple while preserving the boundaries expected in a professional codebase.

## Delivery Principles

- Build behaviour test-first where practical.
- Keep business rules inside the domain, not controllers or UI components.
- Deliver each milestone through small, reviewable commits.
- Require automated tests and a successful pipeline before a milestone closes.
- Record important architecture decisions in the repository.

## Detailed Milestones

Each detailed milestone contains its objective, user stories, technical tasks,
acceptance criteria, required tests, definition of done, and expected commits.

- [Milestone 1: Domain Foundation](milestones/M01-domain-foundation.md)
- [Milestone 2: Application Use Cases](milestones/M02-application-use-cases.md)
- [Milestone 3: Persistence](milestones/M03-persistence.md)
- [Milestone 4: Production REST API](milestones/M04-rest-api.md)
- [Milestone 5: Priority Intelligence](milestones/M05-priority-intelligence.md)
- [Milestone 6: Web Experience](milestones/M06-web-experience.md)
- [Milestone 7: Identity And Collaboration](milestones/M07-identity-collaboration.md)
- [Milestone 8: Delivery And Operations](milestones/M08-delivery-operations.md)
- [Milestone 9: Task Metadata And Account Access](milestones/M09-task-metadata-account-access.md)

## Milestone 0: Planning And Baseline

**Status:** Complete

**Objective:** Establish the product scope, architecture, quality strategy, and
team workflow before restructuring the prototype.

**Deliverables:**

- Product roadmap and prioritised requirements.
- Proposed solution architecture.
- TDD and test strategy.
- Git and contribution workflow.
- Baseline CI pipeline.

**Definition of done:**

- Documents are reviewed and linked from the README.
- Existing behaviour is understood and recorded.
- The next milestone has explicit acceptance criteria.

## Milestone 1: Domain Foundation

**Status:** Complete

**Objective:** Model task-management behaviour using OOP and TDD without
depending on HTTP, databases, or frontend frameworks.

**User value:**

- A task can move safely through its lifecycle.
- Invalid state changes are rejected consistently.
- Priority can be calculated from meaningful planning inputs.

**Deliverables:**

- `TaskItem`, `Project`, and `TaskDependency` domain entities.
- Value objects for title, due date, effort, and priority score.
- Task statuses: Backlog, Ready, In Progress, Blocked, and Completed.
- Domain rules for starting, blocking, completing, and reopening tasks.
- Fast domain unit tests.

**Progress delivered:**

- Guarded `TaskItem` lifecycle with explicit state transitions.
- Dependency management with duplicate and circular-reference protection.
- Effective blocked-state detection for incomplete dependencies.
- Explainable priority score using value, urgency, risk reduction, and effort.
- Project aggregate with guarded edits and archive rules.
- Due-date and Fibonacci effort-estimate value objects.
- Domain events for successful task lifecycle changes.
- 60 domain tests covering success, validation, and rule-failure paths.

**Definition of done:**

- Domain tests describe every supported state transition.
- Domain projects have no web or persistence dependencies.
- Public setters do not bypass business rules.

## Milestone 2: Application Use Cases

**Status:** Complete

**Objective:** Expose the domain through testable application operations.

**Deliverables:**

- Commands for creating, editing, starting, blocking, and completing tasks.
- Queries for task detail, project boards, and filtered task lists.
- DTO mapping, validation, pagination, and cancellation support.
- Interfaces for persistence, identity, and time.
- Application unit tests.

**Progress delivered:**

- Application and Application.Tests project boundaries.
- Typed application results and errors.
- Repository, unit-of-work, identifier, and clock ports.
- Create, start, complete, and add-dependency task commands.
- Task detail and filtered paginated task queries.
- Edit, ready, block, unblock, reopen, scheduling, planning, and dependency
  maintenance commands.
- Project create, update, archive, details, and delivery-board use cases.
- Architecture dependency tests.
- 34 application tests and 62 domain tests.

**Definition of done:**

- Every use case has success and failure tests.
- Application code does not know about ASP.NET Core or a concrete database.

## Milestone 3: Persistence

**Status:** Complete

**Objective:** Persist application data reliably with Entity Framework Core.

**Deliverables:**

- EF Core database context and entity configurations.
- SQLite configuration for local development.
- Migrations and repeatable development seed data.
- Repository implementations and persistence integration tests.
- Production configuration suitable for Azure SQL.

**Progress delivered:**

- EF Core Infrastructure and integration-test projects.
- Explicit project, task, dependency, planning, priority, and activity mappings.
- SQLite migrations and idempotent development seed data.
- Tracked command repositories and no-tracking read repositories.
- Server-side filtering, sorting, pagination, and project-board aggregation.
- Transaction rollback and optimistic-concurrency protection.
- SQLite and Azure SQL provider selection through configuration.
- 16 SQLite integration tests.

**Definition of done:**

- Data survives application restarts.
- Migrations can create a clean database.
- Integration tests verify important mappings and queries.

## Milestone 4: Production REST API

**Status:** Complete

**Objective:** Provide a documented and consistent HTTP interface.

**Deliverables:**

- Versioned endpoints or focused endpoint modules.
- Request and response contracts separated from domain entities.
- Problem Details error responses and request validation.
- OpenAPI documentation, health checks, and structured logging.
- API integration tests for core workflows.

**Progress delivered:**

- Focused endpoint modules under `/api/v1`.
- Separate request contracts mapped to application commands and queries.
- Typed HTTP mapping for success, validation, not-found, and conflict results.
- Central exception handling with safe Problem Details responses.
- OpenAPI route discovery and string enum contracts.
- Correlation identifiers in response headers and structured log scopes.
- Separate process liveness and database readiness endpoints.
- In-process integration tests using migrated SQLite databases.
- Updated Docker, Azure Pipelines, and VS Code HTTP client paths.
- 12 API integration tests and 124 tests across the full solution.

**Definition of done:**

- API contracts are represented in integration tests.
- Invalid requests return predictable status codes and error bodies.
- Health and OpenAPI endpoints work in supported environments.

## Milestone 5: Priority Intelligence

**Status:** Complete

**Objective:** Introduce the feature that differentiates the product.

**Deliverables:**

- Explainable priority score using value, urgency, risk, and effort.
- Dependency graph with automatic blocked-state detection.
- Deadline health indicators.
- Activity history for important task changes.
- Dashboard summary queries.

**Progress delivered:**

- Explainable score, band, effort, and weighted contribution contracts.
- Deadline health with deterministic clock-based boundaries.
- Stable score, due-date, creation-time, and identifier ordering.
- Transitive incomplete dependency chains for high-priority blocked work.
- Immutable actor-attributed task activity history.
- Project risk totals and a cross-project portfolio dashboard.
- Versioned API contracts and persistence/API integration coverage.

**Definition of done:**

- Score inputs and calculation are visible and explainable.
- Circular dependencies are rejected.
- Scoring and dependency rules are covered by unit tests.

## Milestone 6: Web Experience

**Status:** Complete

**Objective:** Deliver an accessible, responsive interface for daily work.

**Deliverables:**

- React and TypeScript application.
- Project dashboard, task list, and Kanban board.
- Task editor with priority inputs and dependency selection.
- Search, filtering, sorting, loading, empty, and error states.
- Component and end-to-end tests for critical journeys.

**Progress delivered:**

- React 19, TypeScript, and Vite application boundary.
- Responsive portfolio health, priority list, and Kanban workspace.
- Task creation, editing, planning, and lifecycle actions.
- Loading, search, empty, error, desktop, and mobile states.
- Vitest component tests and Playwright desktop/mobile journeys.
- ASP.NET Core static hosting plus Docker and Azure Pipeline builds.

**Definition of done:**

- Core workflows work on desktop and mobile viewports.
- Keyboard navigation and accessible labels are verified.
- Frontend build and tests run in CI.

## Milestone 7: Identity And Collaboration

**Status:** Complete

**Objective:** Secure data and support team ownership.

**Deliverables:**

- Authentication and user profiles.
- Workspaces with Owner, Manager, and Member roles.
- Task assignment and project membership.
- Policy-based authorization and security tests.

**Progress delivered:**

- User profiles and workspace membership aggregate.
- Owner, Manager, and Member role invariants.
- EF Core mappings, migrations, and deterministic development identities.
- Production JWT bearer and isolated development authentication.
- Secured workspace membership and task assignment APIs.
- Authenticated project, task, dashboard, and collaboration routes.
- Identity-aware workspace and assignee controls in React.
- Domain, persistence, API security, component, and browser tests.

**Definition of done:**

- Users cannot access another workspace without membership.
- Sensitive operations require the correct role.

## Milestone 8: Delivery And Operations

**Status:** Complete

**Objective:** Make every release repeatable and observable.

**Deliverables:**

- Multi-stage Docker build.
- Azure DevOps build, test, coverage, package, and deployment stages.
- Environment-specific configuration and secret management.
- Azure App Service deployment and smoke test.
- Operational runbook and portfolio architecture diagram.

**Progress delivered:**

- Pipeline coverage publishing for backend tests.
- Docker image build validation and image artifact packaging.
- Deployment smoke-test hook for live and readiness probes.
- Operations runbook covering release, configuration, rollback, and smoke tests.
- Cost-conscious Azure hosting notes for App Service, Azure SQL, and optional
  Docker usage.
- Clickable Activity, Settings, Profile, password, and logout UI paths.
- Task pagination and a useful activity fallback when no activity history is
  available.
- Azure setup checklist covering App Service, optional Azure SQL, service
  connection, pipeline variables, smoke tests, and cost guardrails.
- Deployment and operations architecture diagram.

**Definition of done:**

- A clean checkout builds and tests in CI.
- Deployment requires no manual file changes.
- Production health can be checked after deployment.
- Release, rollback, smoke-test, and Azure setup instructions are documented.

## Initial Commit Sequence

Each commit must leave the repository in a coherent state. The first
implementation milestone is expected to include at least:

1. `docs: define product roadmap and engineering workflow`
2. `test: specify task lifecycle domain behaviour`
3. `feat: implement task lifecycle domain model`
4. `test: specify priority scoring rules`
5. `feat: implement explainable priority scoring`
6. `refactor: establish application solution boundaries`

Commit count is evidence of incremental delivery, not a substitute for useful
history. Tests and implementation may be separated when each commit remains
understandable and buildable.
