# Azure Setup Checklist

## Goal

Use Azure only when the project is ready for portfolio review, keep deployment
manual, and avoid paid resources until they are intentionally enabled.

## Recommended Low-Cost Path

1. Create an Azure budget alert before deploying anything.
2. Create an App Service plan using the F1 Free tier for portfolio testing.
3. Create a Linux Web App on that F1 plan.
4. Keep SQLite for local development.
5. Add Azure SQL only when you want to demonstrate cloud database hosting.
6. Keep Docker optional unless you move to container hosting.
7. Delete, stop, or scale down resources after the review/demo period.

## App Service

Create a Linux Web App with these settings:

| Setting | Value |
| --- | --- |
| Publish | Code |
| Runtime stack | .NET |
| App Service plan | F1 Free for portfolio testing |
| Always On | Off |
| HTTPS Only | On |

Add these app settings:

| Name | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__Provider` | `Sqlite` or `SqlServer` |
| `Authentication__Authority` | Your identity provider authority |
| `Authentication__Audience` | Your API audience |

If using SQLite on App Service for a portfolio demo, treat it as temporary demo
storage. Use Azure SQL for anything that needs durable production data.

## Optional Azure SQL

Use Azure SQL only when the portfolio needs a managed database demonstration.

1. Create an Azure SQL Database using the free offer if available.
2. Keep usage within the free monthly allowance.
3. Store the connection string in App Service configuration or Key Vault.
4. Set `Database__Provider` to `SqlServer`.
5. Set `ConnectionStrings__TodoApp` to the Azure SQL connection string.
6. Run migrations through the pipeline or a controlled admin process.

Do not commit database passwords or publish profiles.

## Azure DevOps Service Connection

1. Open Azure DevOps.
2. Go to **Project settings**.
3. Select **Service connections**.
4. Create an Azure Resource Manager service connection.
5. Scope it to the resource group that contains the Web App.
6. Copy the service connection name into `azure-pipelines.yml`:

```yaml
azureServiceConnection: YOUR-AZURE-SERVICE-CONNECTION
```

## Pipeline Variables

Set these variables in Azure DevOps or update the non-secret placeholders in
`azure-pipelines.yml`:

| Variable | Example |
| --- | --- |
| `azureServiceConnection` | `todoapp-portfolio-connection` |
| `webAppName` | `todoapp-portfolio` |
| `smokeTestBaseUrl` | `https://todoapp-portfolio.azurewebsites.net` |

Run normal pull-request validation with `deployToAzure` set to `false`.
Deploy manually with `deployToAzure` set to `true`.

## Smoke Test

After deployment, run:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/SmokeTest.ps1 `
  -BaseUrl "https://todoapp-portfolio.azurewebsites.net"
```

Expected result:

- `/health/live` succeeds.
- `/health/ready` succeeds.

## Cost Guardrails

- Create a monthly budget alert.
- Keep the App Service on F1 Free unless deliberately scaling.
- Keep Always On disabled on free/shared tiers.
- Avoid paid Azure SQL usage unless needed.
- Keep `deployToAzure` manual.
- Review Azure Cost Management after every deployment test.
