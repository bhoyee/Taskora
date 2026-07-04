# Milestone 5: Priority Intelligence

## Status

Complete

## Objective

Complete the product differentiator by turning domain priority and dependency
data into explainable delivery guidance, deadline health, activity history, and
dashboard summaries.

## User Stories

### M05-US01: Understand Priority

As a contributor, I want to see the factors behind a priority score so that I
understand why work is ranked above other tasks.

### M05-US02: Detect Delivery Risk

As a project manager, I want deadline and blocker indicators so that I can act
before delivery is missed.

### M05-US03: Audit Decisions

As a project manager, I want important task changes recorded so that priority
and delivery decisions can be reviewed.

### M05-US04: View Portfolio Health

As a project manager, I want dashboard summaries so that high-risk work is
visible across projects.

## Technical Tasks

- Expose score contributions and bands through application and API contracts.
- Add deadline-health calculation using due date, status, and current time.
- Add dependency-chain summaries and high-priority blocker detection.
- Persist activity entries derived from domain events.
- Add project and portfolio dashboard queries.
- Add deterministic tie-breaking by score, due date, and creation time.
- Document the scoring formula and interpretation.

## Acceptance Criteria

- Users can see score value, band, effort, and each weighted contribution.
- Deadline health distinguishes healthy, at risk, overdue, and completed work.
- High-priority blocked tasks identify their incomplete prerequisites.
- Activity history records actor, timestamp, action, and relevant changes.
- Activity records cannot be changed through public endpoints.
- Equal scores sort deterministically.
- Dashboard totals match underlying filtered data.

## Required Tests

- Unit tests for all deadline-health boundaries.
- Application tests for priority ordering and tie-breakers.
- Dependency-chain summary tests.
- Activity creation and immutability tests.
- Dashboard aggregation integration tests.
- API contract tests for score explanations and risk indicators.

## Definition Of Done

- Priority recommendations are explainable rather than opaque.
- Deadline and dependency risks are visible through the API.
- Activity history is durable and immutable through public operations.
- Dashboard calculations are covered by automated tests.
- Product and API documentation describe the scoring model.
- A pull request to `dev` passes review.

## Scoring Model

Priority is calculated as:

```text
((business value * 3) + (urgency * 2) + (risk reduction * 2)) / effort
```

Inputs use a 1-5 scale and effort uses the supported Fibonacci estimates.
Scores are rounded to two decimal places. Bands are Low below 3, Medium from
3, High from 6, and Critical from 10.

Equal scores sort by due date, creation time, then identifier. This keeps
recommendations stable while favouring work due sooner and created earlier.

Deadline health is Completed for finished work, Overdue after the due date, At
Risk from the due date through three days before it, and Healthy otherwise.

## Expected Commits

1. `test: specify deadline health rules`
2. `feat: implement deadline health calculation`
3. `test: specify priority ordering and tie breakers`
4. `feat: add explainable priority queries`
5. `test: specify activity history behaviour`
6. `feat: persist immutable task activity`
7. `test: specify dashboard aggregations`
8. `feat: add project delivery dashboard queries`
9. `docs: explain priority intelligence model`
