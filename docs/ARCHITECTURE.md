# Architecture

## Architectural Style

TodoApp will use a modular monolith with domain-oriented boundaries. The
application is deployed as one product, while its code is separated into
projects with explicit dependency rules.

This design demonstrates professional architecture without introducing the
operational cost of microservices before the product needs them.

## Proposed Solution Structure

```text
Taskora.sln
src/
  TodoApp.Domain/
  TodoApp.Application/
  TodoApp.Infrastructure/
  TodoApp.Api/
  TodoApp.Web/
tests/
  TodoApp.Domain.Tests/
  TodoApp.Application.Tests/
  TodoApp.Infrastructure.IntegrationTests/
  TodoApp.Api.IntegrationTests/
docs/
```

## Project Responsibilities

### TodoApp.Domain

- Entities, value objects, domain services, and domain events.
- Task lifecycle, dependencies, and priority rules.
- No references to ASP.NET Core, EF Core, or frontend code.

### TodoApp.Application

- Use cases expressed as commands and queries.
- Ports for persistence, identity, time, and external services.
- Request-independent validation and result models.
- References only the Domain project.

### TodoApp.Infrastructure

- EF Core database context and mappings.
- Implementations of application ports.
- Database migrations and external integrations.
- References Application and Domain.

### TodoApp.Api

- HTTP endpoints, authentication, authorization, and composition root.
- API contracts, middleware, OpenAPI, health checks, and logging.
- References Application and Infrastructure.

### TodoApp.Web

- React and TypeScript user interface.
- Communicates with the application only through public API contracts.

## Dependency Direction

```text
Web -> API -> Application -> Domain
             ^
             |
       Infrastructure
```

Infrastructure implements interfaces owned by the Application layer. Domain
code remains independent of delivery and persistence technology.

## Domain Model Direction

The model will use behaviour-rich objects instead of public property setters.
Representative operations include:

```csharp
task.MoveToReady();
task.Start();
task.Block(reason);
task.Complete(completedAt);
task.Reopen();
task.AddDependency(dependency);
```

Each operation protects its invariants. Controllers translate HTTP input into
application use cases; they do not implement business rules.

## Initial Aggregate Boundaries

### TaskItem

Owns title, description, workflow state, planning inputs, due date, completion
information, and direct dependencies.

### Project

Owns project identity, descriptive information, target date, and archive state.

Workspace and identity boundaries will be introduced in the collaboration
milestone after the core task model is stable.

## Data Strategy

- SQLite for simple local development.
- EF Core migrations for schema evolution.
- Azure SQL for the hosted environment.
- UTC timestamps at persistence and API boundaries.
- Development seed data separated from production startup.

## API Strategy

- Resource-oriented HTTP endpoints.
- DTOs separate from domain and persistence models.
- Problem Details for consistent errors.
- Server-side filtering, sorting, and pagination.
- OpenAPI as the discoverable contract.
- Versioning introduced before incompatible public changes.

## Cross-Cutting Concerns

- Dependency injection at the API composition root.
- Structured logging and correlation identifiers.
- Central exception handling.
- Authentication and policy-based authorization.
- Configuration through environment-specific providers.
- Secrets supplied by development secret storage or Azure configuration.

## Architecture Decision Records

Material decisions will be added under `docs/decisions/` using this format:

```text
ADR-0001-short-decision-name.md
```

Each record will state context, decision, alternatives, and consequences.

## First Decisions

1. Use a modular monolith instead of microservices.
2. Use a behaviour-rich domain model for core rules.
3. Use React and TypeScript for the web client.
4. Use EF Core with SQLite locally and Azure SQL when hosted.
5. Keep the application layer independent of ASP.NET Core.
