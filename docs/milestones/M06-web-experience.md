# Milestone 6: Web Experience

## Status

Complete

## Objective

Deliver an accessible, responsive React and TypeScript workspace for daily
project planning, priority review, and task delivery.

## User Stories

- As a contributor, I can find and update prioritized work efficiently.
- As a project manager, I can compare portfolio risks and task explanations.
- As a contributor, I can move work through its valid lifecycle.
- As a mobile user, I can complete core workflows without page overflow.

## Technical Tasks

- Create the React, TypeScript, and Vite project boundary.
- Add API contracts for dashboard, task search, creation, editing, and planning.
- Build list and Kanban views with responsive navigation.
- Add loading, empty, error, search, dialog, and lifecycle states.
- Add component and browser tests.
- Serve the production SPA through ASP.NET Core.
- Build and test the frontend in Docker and Azure Pipelines.

## Acceptance Criteria

- Portfolio health and explainable priority data are visible.
- Tasks can be searched, created, edited, planned, and advanced.
- List and Kanban views work with keyboard and pointer input.
- Desktop and mobile layouts have no page-level horizontal overflow.
- Loading, empty, and API failure states are coherent.
- The production API artifact contains the compiled web application.

## Required Tests

- Component tests for loading data, switching views, and opening forms.
- End-to-end tests for task inspection and keyboard board interaction.
- Desktop and mobile browser projects.
- Production frontend and ASP.NET Core publish checks.

## Definition Of Done

- Frontend unit and browser tests pass.
- Backend regression tests pass.
- npm and NuGet vulnerability audits are clean.
- Docker and Azure Pipelines build the web application.
- Responsive browser screenshots have been reviewed.

## Expected Commits

1. `feat: establish responsive React delivery workspace`
2. `fix: align web task query with API pagination`
3. `feat: add task planning and lifecycle workflows`
4. `test: cover accessible frontend journeys`
5. `ci: deliver web application with production API`
6. `docs: complete web experience milestone`
