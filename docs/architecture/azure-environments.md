# Azure Environments

This document defines the intended Azure portal resources and synchronization
model for the project environments.

## Scope

- `develop` deploys to a development Azure environment.
- `master` is the stable common baseline and does not deploy a concrete client.
- Every client uses `release/<tenant>` and a separate Azure account or
  subscription.
- Development and tenant environments use the same application architecture
  unless a tenant requirement explicitly says otherwise.
- Environment-specific values, secrets, resource names, role invitations, and
  domains must not be committed to the repository.

## Current Repository State

- The common GitHub Actions workflow deploys only pushes to `develop`.
- Tenant deployment workflows exist only in their `release/<tenant>` branches
  and authenticate with tenant-specific SWA deployment tokens.
- The workflow deploys the Angular/Ionic frontend from `frontend`.
- The workflow deploys the managed Static Web Apps API from `functions`.
- The deployed frontend output is `dist/app/browser`.
- The workflow uses a Static Web Apps deployment token stored in GitHub
  Secrets as `AZURE_STATIC_WEB_APPS_API_TOKEN_BLACK_POND_085834203`.
- Initial infrastructure-as-code files are present under `infra/`.
- `infra/main.bicep` is the Azure resource baseline for `develop` and every
  tenant environment.
- `docs/process/deploy-azure-tenant.md` is the canonical approval-gated tenant
  deployment procedure.

## Current Development Azure Inventory

The current `develop` Azure environment has been inspected through Azure CLI.

Subscription:

- Name: `Subscription 1978`.
- ID: `33427c73-b710-48a3-99a2-82217072bd94`.
- Tenant: `Default Directory`.
- Tenant ID: `9ee559cb-963e-4272-a38a-493dd974dd2a`.
- Tenant domain: `myserver1978Gmail.onmicrosoft.com`.

Resource group:

- Name: `rg-simple-discount-verifier`.
- Location: `westeurope`.

Resources:

| Resource                               | Type                                       | Location     | Notes                                               |
| -------------------------------------- | ------------------------------------------ | ------------ | --------------------------------------------------- |
| `swa-simple-discount-verifier`         | `Microsoft.Web/staticSites`                | `westeurope` | Static Web App for `develop`.                       |
| `sdvstorageaccount`                    | `Microsoft.Storage/storageAccounts`        | `westeurope` | Table Storage account.                              |
| `ws-simple-discount-verifier`          | `Microsoft.OperationalInsights/workspaces` | `westeurope` | Log Analytics workspace for Application Insights.   |
| `ins-simple-discount-verifier`         | `microsoft.insights/components`            | `westeurope` | Workspace-based Application Insights component.     |
| `Application Insights Smart Detection` | `microsoft.insights/actiongroups`          | `global`     | Automatically created Smart Detection action group. |

Static Web App:

- Name: `swa-simple-discount-verifier`.
- Default hostname: `black-pond-085834203.7.azurestaticapps.net`.
- SKU: `Free`.
- Provider: `GitHub`.
- Repository: `https://github.com/PavelShvykyj/Simple-discount-verifier`.
- Branch: `develop`.
- Staging environment policy: `Enabled`.

Configured Static Web Apps application settings:

- `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- `AppStorageConnectionString`.
- `ScannerSurveyTableName`.

Configured Static Web Apps users and roles:

| Provider | Roles                                 |
| -------- | ------------------------------------- |
| `aad`    | `admin`, `anonymous`, `authenticated` |

Storage Account:

- Name: `sdvstorageaccount`.
- SKU: `Standard_LRS`.
- Kind: `StorageV2`.
- Access tier: `Hot`.
- HTTPS-only traffic: enabled.
- Minimum TLS version: `TLS1_2`.
- Public network access: `Enabled`.
- Blob public access: disabled.

Current tables:

- `ScannerSurveyResults`.
- `CustomerProfiles`.
- `DiscountRuntime`.
- `AuditEvents`.

Application Insights:

- Name: `ins-simple-discount-verifier`.
- Application type: `web`.
- Workspace:
  `/subscriptions/33427c73-b710-48a3-99a2-82217072bd94/resourceGroups/rg-simple-discount-verifier/providers/Microsoft.OperationalInsights/workspaces/ws-simple-discount-verifier`.
- Public network access for ingestion: `Enabled`.
- Public network access for query: `Enabled`.
- Current retention: 30 days.
- Current daily data cap: 100 MB/day, represented as
  `DataVolumeCap.Cap = 0.1` on the Application Insights
  `CurrentBillingFeatures` child resource.
- `az monitor app-insights component show` may still report
  `dailyDataCapInGB: null`; verify the cap through
  `Microsoft.Insights/components/CurrentBillingFeatures`.

Application Insights differences from the target tenant-production baseline:

- The existing Smart Detection action group has no email receivers configured.
- No metric alert rules are currently configured.
- No scheduled query alert rules are currently configured.

## Required Azure Resources Per Environment

Each environment requires:

- Resource group.
- Azure Static Web App.
- Managed Static Web Apps Functions runtime configured as
  `dotnet-isolated:8.0`.
- Azure Storage Account for Table Storage.
- Azure Table Storage tables:
  - `ScannerSurveyResults`;
  - `CustomerProfiles`;
  - `DiscountRuntime`;
  - `AuditEvents`.
- Application Insights for backend Azure Functions/API telemetry.
- Static Web Apps built-in authentication through `/.auth/*`.
- Microsoft Entra ID provider `aad`.
- Static Web Apps custom role `admin`.
- Static Web Apps role invitations for concrete administrators.
- GitHub Actions deployment secret for that environment's Static Web App.

The MVP intentionally does not require a separate Azure Function App, frontend
MSAL configuration, a dedicated Angular SPA App Registration, or Managed
Identity.

## Static Web Apps Configuration

The source of truth for route protection and managed Functions runtime is
`frontend/public/staticwebapp.config.json`.

Important route rules:

- `/scanner-survey*` requires the Static Web Apps custom role `admin`.
- `/admin*` requires the Static Web Apps custom role `admin`.
- `/api/backoffice/*` requires the Static Web Apps custom role `admin`.
- `/api/scanner-survey` requires the Static Web Apps custom role `admin`.
- `/api/pos/*` allows `anonymous` at the Static Web Apps edge and must be
  authenticated inside Azure Functions with HMAC headers.
- `/api/public/*` allows `anonymous` and `authenticated`.

The `admin` role is a Static Web Apps custom role. It is not an Azure RBAC role
and not a Microsoft Entra group.

## Storage Configuration

The backend accesses Azure Table Storage through server-side configuration.

Required settings:

```text
AppStorageConnectionString=<storage account connection string>
ScannerSurveyTableName=ScannerSurveyResults
```

The scanner survey function creates its table if it does not already exist.
Production tables should still be treated as required environment resources so
their existence is visible in portal inventory and future automation.

Business data tables:

- `CustomerProfiles` stores saved customer profiles keyed by normalized phone.
- `DiscountRuntime` stores current short-lived redemption state.
- `AuditEvents` stores append-only business and support audit events.

Temporary scanner compatibility data:

- `ScannerSurveyResults` stores scanner survey submissions and admin
  browser-camera checks.

## Observability Configuration

Application Insights is enabled for backend Azure Functions/API telemetry only.
Frontend/browser telemetry is out of scope for the tenant-production baseline.

Application Insights configuration:

- retention: 30 days;
- daily cap: 100 MB/day;
- sampling: enabled for backend telemetry;
- alerts: email-based.

Required alert coverage:

- unhandled backend exceptions;
- elevated HTTP 5xx rate;
- elevated backend request latency;
- SMS provider failures;
- Azure Table Storage dependency failures;
- availability/health endpoint failure.

Telemetry rules:

- Application Insights is operational telemetry only.
- Business events stay in `AuditEvents`.
- Do not send raw request bodies, SMS codes, barcode values, raw phone numbers,
  or other sensitive customer data to Application Insights.
- Include `correlationId` in backend telemetry whenever one exists so
  operational telemetry can be matched with `AuditEvents`.

## Server-Side Secrets And Settings

These values are environment-specific and must be stored in Azure Static Web
Apps/API configuration or another approved server-side secret store:

- `AppStorageConnectionString`.
- `ScannerSurveyTableName`.
- POS HMAC secret for `main-pos-system`.
- Phone runtime key secret used to derive opaque phone runtime keys.
- SMS code hashing secret.
- Barcode hashing secret.
- Audit phone hashing secret.
- SMS-Fly credentials and sender configuration.
- Any future Application Insights connection string or instrumentation setting
  required by the hosting model.

No secret may be placed in Angular code, frontend environment files, or
browser-delivered assets.

## Environment Matrix

| Environment       | Git branch         | Azure account/subscription                                   | Resource group                | Static Web App                          | Storage account      | Application Insights           | GitHub secret                                          |
| ----------------- | ------------------ | ------------------------------------------------------------ | ----------------------------- | --------------------------------------- | -------------------- | ------------------------------ | ------------------------------------------------------ |
| Development       | `develop`          | `Subscription 1978` / `33427c73-b710-48a3-99a2-82217072bd94` | `rg-simple-discount-verifier` | `swa-simple-discount-verifier`          | `sdvstorageaccount`  | `ins-simple-discount-verifier` | `AZURE_STATIC_WEB_APPS_API_TOKEN_BLACK_POND_085834203` |
| Stable baseline   | `master`           | None                                                         | None                          | None                                    | None                 | None                           | None                                                   |
| Tenant production | `release/<tenant>` | Separate tenant account/subscription                         | `rg-simple-discount-verifier` | `swa-simple-discount-verifier-<tenant>` | `sdvstorage<tenant>` | `ins-simple-discount-verifier` | `AZURE_STATIC_WEB_APPS_API_TOKEN_<TENANT>`             |

## Manual Portal Steps To Confirm

For each environment:

1. Confirm the Azure account/subscription.
2. Confirm or create the resource group.
3. Confirm or create the Static Web App.
4. Confirm or create the Storage Account.
5. Confirm or create the required Azure Tables.
6. Configure Static Web Apps application settings and secrets.
7. Configure Static Web Apps Authentication with Microsoft Entra ID provider.
8. Invite concrete administrators to the Static Web Apps `admin` custom role.
9. Configure Application Insights retention, daily cap, sampling, and alerts.
10. Add the environment-specific deployment secret to GitHub Actions.
11. Deploy from the matching branch and verify the public app, protected admin
    route, scanner survey API, and backend telemetry.

## Automation Direction

Use Bicep for repeatable Azure resource creation and updates. The current
repository shape is:

```text
infra/
  main.bicep
  scripts/
    New-TenantDeploymentFiles.ps1
  templates/
    azure-static-web-apps-tenant.yml
  parameters/
    develop.example.json
    production.example.json
    tenants/
      <tenant>.json
```

The current stepper covers:

- release branch and tenant deployment-file generation;
- empty Static Web App creation without GitHub mutation;
- resource group-level Bicep what-if/deployment;
- Storage Account and Azure Table creation;
- Application Insights retention and daily cap;
- runtime settings from local secret variables;
- tenant-specific GitHub Actions workflow;
- cleanup endpoint verification and Logic App scheduler.

Automation may still leave these as documented manual steps if Azure tooling
does not support them cleanly for this project phase:

- entering real secret values;
- Static Web Apps `admin` role invitations;
- final custom domain and DNS validation, if custom domains are added later.

## Next Decisions

- Decide per tenant whether custom domain and Standard SWA SKU are required.
- Decide whether tenant alerts should be enabled and which operations email
  receives them.
- Revisit deployment-token authentication only if the number of tenants or
  token-rotation burden justifies a different CI/CD model.
