# Milestone 8: Delivery And Operations

## Status

In progress

## Objective

Make every release repeatable, observable, and safe to validate after
deployment.

## User Stories

- As a developer, I can prove a clean checkout builds and tests in CI.
- As a reviewer, I can see test coverage and packaged release artifacts.
- As an operator, I can deploy the same package that passed CI.
- As an operator, I can run a smoke test against production health endpoints.
- As a portfolio reviewer, I can understand the release process from the repo.

## Technical Tasks

- Publish backend test results and coverage from Azure Pipelines.
- Build and publish the production web artifact.
- Validate the Docker image during CI.
- Package the deployable API artifact and Docker image artifact.
- Add an operator runbook for configuration, release, rollback, and smoke tests.
- Add a reusable smoke-test script for live and readiness endpoints.
- Document environment variables and secret boundaries.

## Acceptance Criteria

- CI restores, builds, tests, and publishes the .NET solution.
- CI restores, tests, and builds the React frontend.
- CI publishes coverage data when tests complete.
- CI validates that the Dockerfile can build from a clean checkout.
- Deployment remains manually gated until Azure App Service details are set.
- Smoke tests can be run locally or by the pipeline against a deployed base URL.

## Required Tests

- Existing .NET unit and integration tests.
- Existing frontend component tests.
- Docker build validation in CI.
- Smoke test against `/health/live` and `/health/ready`.

## Definition Of Done

- A new developer can understand how releases are produced.
- Azure deployment configuration is documented without committed secrets.
- CI produces both application and Docker artifacts.
- Production health can be checked after deployment.
- The milestone branch is pushed and reviewed through a pull request.

## Expected Commits

1. `docs: define delivery operations milestone`
2. `ci: publish coverage and validate docker image`
3. `ops: add deployment smoke test runbook`
4. `docs: complete delivery and operations milestone`
