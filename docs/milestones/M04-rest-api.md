# Milestone 4: Production REST API

## Status

Complete

## Objective

Replace the prototype endpoints with a versioned, documented, observable, and
consistently tested HTTP interface over application use cases.

## User Stories

### M04-US01: Integrate Through HTTP

As an API consumer, I want stable task and project contracts so that clients can
integrate without depending on internal domain models.

### M04-US02: Understand Request Failures

As an API consumer, I want consistent validation and error responses so that I
can correct requests reliably.

### M04-US03: Operate The Service

As an operator, I want health checks, structured logs, and request correlation
so that failures can be detected and diagnosed.

## Technical Tasks

- Move the root prototype into `src/Taskora.Api`.
- Add API request and response contracts separate from domain entities.
- Add versioned controllers or focused endpoint modules.
- Map application outcomes to HTTP status codes.
- Add central exception handling using Problem Details.
- Add request validation, OpenAPI metadata, and examples.
- Add health endpoints, structured logging, and correlation identifiers.
- Create `Taskora.Api.IntegrationTests` with an in-process test server.
- Update Docker and Azure pipeline project paths.

## Acceptance Criteria

- Task and project operations are available under a versioned route.
- Create responses return `201 Created` with a resource location.
- Missing resources return `404`.
- Validation and domain conflicts return consistent Problem Details bodies.
- API contracts never expose EF Core entities.
- OpenAPI describes all supported endpoints and response types.
- Health endpoints distinguish process health from dependency readiness.
- Correlation identifiers appear in logs and responses.

## Required Tests

- Integration tests for each endpoint success path.
- Validation, not-found, conflict, and malformed-request tests.
- Response-contract and content-type tests.
- OpenAPI and health endpoint smoke tests.
- Logging/correlation middleware tests where practical.
- Regression test for the original create-to-complete workflow.

## Definition Of Done

- Prototype in-memory endpoints are removed.
- API integration tests pass in CI.
- OpenAPI can be used to discover and exercise the API.
- Errors are safe and consistent in production mode.
- Docker publish and Azure artifact generation still succeed.
- A pull request to `dev` passes review.

## Expected Commits

1. `refactor: move API into source project boundary`
2. `test: specify task endpoint contracts`
3. `feat: expose versioned task endpoints`
4. `test: specify project endpoint contracts`
5. `feat: expose versioned project endpoints`
6. `feat: add problem details and request validation`
7. `feat: add health checks logging and correlation`
8. `test: cover OpenAPI and operational endpoints`
9. `ci: publish relocated API project`
10. `docs: complete production API milestone`
