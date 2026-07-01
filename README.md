# TodoApp

A priority-intelligence task management application built with C# and .NET.
The current code is a minimal API prototype; the planned product will help
users rank work using value, urgency, risk, effort, and task dependencies.

## Project Documentation

- [Product roadmap](docs/ROADMAP.md)
- [Product requirements](docs/REQUIREMENTS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Testing strategy](docs/TESTING.md)
- [Contribution workflow](docs/CONTRIBUTING.md)

Development follows incremental delivery, TDD for core behaviour, and a
modular monolith architecture. Milestone 0 establishes the planning baseline;
Milestone 1 introduces the first domain model and tests.

## Current Development

Milestone 1 is being developed on `feature/domain-foundation`. The domain layer
currently includes:

- A guarded task lifecycle from Backlog to Completed.
- Blocking, unblocking, and reopening rules.
- Task dependencies with circular-reference protection.
- Automatic detection of work blocked by incomplete dependencies.
- Explainable priority scoring using value, urgency, risk reduction, and effort.
- 30 xUnit domain tests.

Run the complete build and test suite with:

```powershell
dotnet build TodoApp.sln --configuration Release
dotnet test TodoApp.sln --configuration Release --no-build
```

## Current Prototype

- `GET /todos` returns all todo items.
- `GET /todos/{id}` returns one todo item.
- `POST /todos` creates a todo item.
- `PUT /todos/{id}` updates a todo item.
- `DELETE /todos/{id}` deletes a todo item.

The app currently uses an in-memory list, so data resets every time the app restarts.

## Run Locally

Install the .NET SDK, then run:

```bash
dotnet restore
dotnet run
```

The project is configured to run on:

```text
http://localhost:5148
https://localhost:7289
```

You can test the API from `TodoApp.http` in VS Code with the REST Client extension.

## Example Requests

Create a todo:

```http
POST http://localhost:5148/todos
Content-Type: application/json

{
  "title": "Learn Azure DevOps pipelines"
}
```

Update a todo:

```http
PUT http://localhost:5148/todos/1
Content-Type: application/json

{
  "title": "Create my first todo API",
  "isCompleted": true
}
```

## Push To GitHub

Install Git first if the `git` command is not available.

```bash
git init
git add .
git commit -m "Create simple todo API"
git branch -M main
git remote add origin https://github.com/YOUR-USERNAME/TodoApp.git
git push -u origin main
```

Replace `YOUR-USERNAME` with your GitHub username.

## Azure DevOps CI Pipeline

This repository includes `azure-pipelines.yml`. It does the following when code is pushed to `main`:

- Installs the .NET SDK.
- Restores NuGet packages.
- Builds the app.
- Publishes the app as a build artifact named `drop`.
- Optionally deploys the artifact to Azure App Service.

To connect it in Azure DevOps:

1. Create a project in Azure DevOps.
2. Go to **Pipelines**.
3. Select **New pipeline**.
4. Select **GitHub** as the code source.
5. Authorize Azure DevOps to access your GitHub repository.
6. Select this repository.
7. Choose **Existing Azure Pipelines YAML file**.
8. Select `/azure-pipelines.yml`.
9. Run the pipeline.

## Optional Deployment

After the CI pipeline works, you can enable the deployment stage. You will need:

- An Azure subscription.
- An App Service already created.
- An Azure DevOps service connection.
- The App Service name.

Then update these variables in `azure-pipelines.yml`:

```yaml
azureServiceConnection: YOUR-AZURE-SERVICE-CONNECTION
webAppName: YOUR-AZURE-WEB-APP-NAME
```

When you manually run the pipeline, set `Deploy to Azure App Service` to `true`.
