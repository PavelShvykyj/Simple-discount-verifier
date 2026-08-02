# Azure Infrastructure

This folder contains the first infrastructure-as-code baseline for the Azure
resources described in `docs/architecture/azure-environments.md`.

## Files

- `main.bicep` creates or updates the Azure resources for one environment.
- `parameters/develop.example.json` matches the currently inspected
  development environment.
- `parameters/production.example.json` records the first tenant production
  baseline and is retained for compatibility.
- `scripts/New-TenantDeploymentFiles.ps1` generates a tenant parameter file
  and deployment workflow from the project naming rules.
- `templates/azure-static-web-apps-tenant.yml` is the inactive workflow
  template used by that generator.

For every new tenant, use the approval-gated runbook in
`docs/process/deploy-azure-tenant.md`. It supersedes the old assumption that
`master` deploys one shared production pilot.

## What The Template Manages

- Azure Static Web App.
- Azure Storage Account.
- Azure Table Storage tables:
  - `ScannerSurveyResults`;
  - `CustomerProfiles`;
  - `DiscountRuntime`;
  - `AuditEvents`.
- Log Analytics workspace.
- Workspace-based Application Insights.
- Application Insights retention.
- Application Insights daily cap.
- Optional email action group.
- Optional scheduled query alerts for:
  - backend exceptions;
  - HTTP 5xx responses;
  - elevated backend latency;
  - Azure Table dependency failures;
  - SMS provider dependency failures.
- Optional Logic App cleanup scheduler for calling the maintenance cleanup
  endpoint on a recurrence.

## What Stays Manual For Now

- Real secret values.
- Static Web Apps `admin` role invitations.
- GitHub Actions secrets.
- Custom domains and DNS validation.
- Availability/health alert wiring; the endpoint exists, but the alert remains
  an optional per-tenant decision.
- Enabling the cleanup Logic App until the backend maintenance cleanup endpoint
  exists and has been smoke-tested.

## Static Web Apps App Settings

`main.bicep` can manage Static Web Apps app settings, but this is disabled by
default:

```json
"manageStaticWebAppSettings": {
  "value": false
}
```

Keep it disabled until all required secret values are supplied through a secure
parameter mechanism. This avoids accidentally replacing existing working
settings with empty values.

The Static Web App resource itself is also disabled by default for the current
`develop` environment:

```json
"manageStaticWebAppResource": {
  "value": false
}
```

Keep it disabled for an already connected SWA unless changing SWA resource
properties is intentional. The current `develop` deployment should create
missing tables and adjust Application Insights without touching
`swa-simple-discount-verifier`.

The current Azure resource reports `buildProperties` as `null`, while the
working GitHub Actions workflow passes these values directly to
`Azure/static-web-apps-deploy@v1`:

- `appLocation`: `./frontend`;
- `apiLocation`: `./functions`;
- `outputLocation`: `/dist/app/browser`.

Keep the same values in Bicep for newly created Static Web Apps. If SWA
resource management is enabled for an already connected app, `what-if` may show
these `buildProperties` being added to the resource. That change should be
treated as intentional only when we are deliberately bringing the SWA resource
under Bicep management.

The current `develop` Static Web App ARM state also includes service-managed
properties such as:

- `deploymentAuthPolicy`: `GitHub`;
- `stableInboundIP`;
- `trafficSplitting.environmentDistribution.default`: `100`.

These properties can appear in `what-if` when the existing SWA resource is
declared from Bicep. Some of them are service-managed and should not be chased
by adding them blindly to the template. For existing environments, keep
`manageStaticWebAppResource` disabled unless the current task is specifically
to change SWA resource properties.

For a future SWA resource change:

1. Keep normal infrastructure deployments on `manageStaticWebAppResource=false`.
2. Decide the exact SWA property that must change.
3. Enable `manageStaticWebAppResource=true` in a temporary local parameter file.
4. Run `az deployment group what-if`.
5. Apply only when the SWA diff contains the intended change and no unexpected
   service-managed property removals.
6. If Bicep cannot express the change without unrelated SWA drift, use a
   targeted Azure CLI or Portal change and then record the decision in
   `docs/architecture/azure-environments.md`.

For a brand-new tenant, the validated runbook creates an empty SWA separately
with Azure CLI and keeps `manageStaticWebAppResource=false`. This prevents
Azure from creating or changing GitHub workflows. Enable Bicep SWA management
only for an intentional SWA resource change after reviewing `what-if`.

When enabled, the template expects secure values for:

- `appStorageConnectionString`;
- `applicationInsightsConnectionString`;
- `mainPosSystemHmacSecret`;
- `phoneRuntimeKeySecret`;
- `smsCodeHashSecret`;
- `barcodeHashSecret`;
- `auditPhoneHashSecret`;
- `cleanupAutomationKey`, when the maintenance cleanup endpoint or cleanup
  scheduler is enabled;
- `smsFlyApiKey`.

It also writes non-secret runtime settings:

- `ScannerSurveyTableName`;
- `CustomerProfilesTableName`;
- `DiscountRuntimeTableName`;
- `AuditEventsTableName`;
- `SmsMaxPerHour`;
- `SmsMaxPerDay`;
- `SmsResponseFloorMilliseconds`.

For an already connected Static Web App, prefer adding settings with Azure CLI
instead of enabling `manageStaticWebAppSettings`. The CLI command updates named
settings without replacing the whole app-settings object.

Safe non-secret settings for the current `develop` environment:

```powershell
az staticwebapp appsettings set `
  --name swa-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --setting-names `
    CustomerProfilesTableName=CustomerProfiles `
    DiscountRuntimeTableName=DiscountRuntime `
    AuditEventsTableName=AuditEvents `
    PosMainClientId=main-pos-system `
    PosRequestFreshnessToleranceSeconds=300 `
    SmsCodeTtlSeconds=180 `
    SmsRetryAfterSeconds=180 `
    SmsMaxPerHour=5 `
    SmsMaxPerDay=10 `
    SmsResponseFloorMilliseconds=1500 `
    BarcodeTtlSeconds=180 `
    DiscountRuntimeRetentionHours=24 `
    AuditEventsRetentionDays=30
```

To create all planned setting names at once and reduce manual typing mistakes,
use this bootstrap command. Placeholder secret values must be replaced before
the related APIs are used:

```powershell
az staticwebapp appsettings set `
  --name swa-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --setting-names `
    CustomerProfilesTableName=CustomerProfiles `
    DiscountRuntimeTableName=DiscountRuntime `
    AuditEventsTableName=AuditEvents `
    PosMainClientId=main-pos-system `
    PosRequestFreshnessToleranceSeconds=300 `
    SmsCodeTtlSeconds=180 `
    SmsRetryAfterSeconds=180 `
    SmsMaxPerHour=5 `
    SmsMaxPerDay=10 `
    SmsResponseFloorMilliseconds=1500 `
    BarcodeTtlSeconds=180 `
    DiscountRuntimeRetentionHours=24 `
    AuditEventsRetentionDays=30 `
    PosMainClientHmacSecret=CHANGE_ME_MANUALLY `
    PhoneRuntimeKeySecret=CHANGE_ME_MANUALLY `
    SmsCodeHashSecret=CHANGE_ME_MANUALLY `
    BarcodeHashSecret=CHANGE_ME_MANUALLY `
    AuditPhoneHashSecret=CHANGE_ME_MANUALLY `
    CleanupAutomationKey=CHANGE_ME_MANUALLY `
    SmsFlyApiKey=CHANGE_ME_MANUALLY `
    SmsFlySender=CHANGE_ME_MANUALLY
```

The bootstrap command intentionally does not set these existing critical
connection settings:

- `AppStorageConnectionString`;
- `APPLICATIONINSIGHTS_CONNECTION_STRING`;
- `ScannerSurveyTableName`.

Do not overwrite connection strings with placeholder values in an environment
that already works. Azure CLI also prints Static Web Apps app-setting values as
`null`; that is value masking, not proof that the setting is empty.

Set or replace connection settings only with real values:

```powershell
$appStorageConnectionString = "<real-storage-connection-string>"
$applicationInsightsConnectionString = "<real-application-insights-connection-string>"

az staticwebapp appsettings set `
  --name swa-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --setting-names `
    AppStorageConnectionString="$appStorageConnectionString" `
    APPLICATIONINSIGHTS_CONNECTION_STRING="$applicationInsightsConnectionString" `
    ScannerSurveyTableName=ScannerSurveyResults
```

Verify setting names without printing secret values:

```powershell
$settings = az staticwebapp appsettings list `
  --name swa-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --output json | ConvertFrom-Json

$settings.properties.PSObject.Properties.Name
```

Secret settings must be added from a local secure source and must not be
committed or pasted into chat/logs:

- `PosMainClientHmacSecret`;
- `PhoneRuntimeKeySecret`;
- `SmsCodeHashSecret`;
- `BarcodeHashSecret`;
- `AuditPhoneHashSecret`;
- `CleanupAutomationKey`;
- `SmsFlyApiKey`;
- `SmsFlySender`, if the sender value is operationally sensitive.

### Secret App Settings

Set these on the Azure Static Web App under
**Settings -> Environment variables**, or with Azure CLI
`az staticwebapp appsettings set`.

| Setting                                 | Purpose                                                                                                             | Source                                                                                                                       |
| --------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `AppStorageConnectionString`            | Lets managed Functions access Azure Table Storage. Already configured in `develop`; required in every environment.  | Azure Storage Account access key connection string.                                                                          |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connects backend telemetry to Application Insights. Already configured in `develop`; required in every environment. | Application Insights connection string.                                                                                      |
| `PosMainClientHmacSecret`               | Shared secret for validating POS HMAC requests from `main-pos-system`.                                              | Generate a long random secret and provision the same value to the POS integration owner.                                     |
| `PhoneRuntimeKeySecret`                 | Derives deterministic opaque phone runtime keys without exposing raw phone numbers.                                 | Generate a long random backend-only secret per environment.                                                                  |
| `SmsCodeHashSecret`                     | Hashes SMS codes so raw SMS codes are not stored.                                                                   | Generate a long random backend-only secret per environment.                                                                  |
| `BarcodeHashSecret`                     | Hashes barcode values so raw active barcodes are not stored.                                                        | Generate a long random backend-only secret per environment.                                                                  |
| `AuditPhoneHashSecret`                  | Hashes phone values for `AuditEvents` diagnostics without storing raw phones.                                       | Generate a long random backend-only secret per environment.                                                                  |
| `CleanupAutomationKey`                  | Authorizes the scheduled Logic App call to the maintenance cleanup endpoint. This is not a POS credential.          | Generate a long random cleanup-only secret per environment. Store the same value in the Logic App secure workflow parameter. |
| `SmsFlyApiKey`                          | Authenticates requests to SMS-Fly.                                                                                  | SMS-Fly account/API credentials.                                                                                             |
| `SmsFlySender`                          | Sender id/name used for SMS messages.                                                                               | SMS-Fly-approved sender value. Treat as sensitive if the provider or operations policy requires it.                          |

Recommended random secret generation from PowerShell:

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
[Convert]::ToBase64String($bytes)
```

For local one-time setting, store values in PowerShell variables first so the
command pattern can be reused without writing secrets to repository files. Use
this command to replace placeholder values created by the bootstrap command:

```powershell
$posSecret = "<secure-pos-hmac-secret>"
$phoneRuntimeSecret = "<secure-phone-runtime-key-secret>"
$smsCodeSecret = "<secure-sms-code-hash-secret>"
$barcodeSecret = "<secure-barcode-hash-secret>"
$auditPhoneSecret = "<secure-audit-phone-hash-secret>"
$cleanupAutomationKey = "<secure-cleanup-automation-key>"
$smsFlyApiKey = "<secure-sms-fly-api-key>"
$smsFlySender = "<sms-fly-sender>"
```

Then apply them:

```powershell
az staticwebapp appsettings set `
  --name swa-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --setting-names `
    PosMainClientHmacSecret="$posSecret" `
    PhoneRuntimeKeySecret="$phoneRuntimeSecret" `
    SmsCodeHashSecret="$smsCodeSecret" `
    BarcodeHashSecret="$barcodeSecret" `
    AuditPhoneHashSecret="$auditPhoneSecret" `
    CleanupAutomationKey="$cleanupAutomationKey" `
    SmsFlyApiKey="$smsFlyApiKey" `
    SmsFlySender="$smsFlySender"
```

For production, use the production Static Web App name and resource group, and
use different secret values from `develop`.

Verify only setting names, not values:

```powershell
$settings = az staticwebapp appsettings list `
  --name swa-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --output json | ConvertFrom-Json

$settings.properties.PSObject.Properties.Name
```

## Cleanup Scheduler

The backend cleanup design has two entrypoints:

- `POST /api/backoffice/system/cleanup` for manual admin use from the Angular
  service page. This route is protected by the Static Web Apps `admin` role.
- `POST /api/system/maintenance/cleanup` for scheduled automation. This route
  is called by Logic App and authorized with `x-cleanup-key`.

The cleanup endpoint must remain narrow: it should not accept table names,
arbitrary cutoff dates, or "delete all" flags. Retention is controlled only by
server-side app settings:

- `DiscountRuntimeRetentionHours`;
- `AuditEventsRetentionDays`.

### Generate Cleanup Key

Generate a separate key per environment. Do not reuse POS secrets.

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
$cleanupAutomationKey = [Convert]::ToBase64String($bytes)
```

Apply it to the Static Web App only after the backend cleanup endpoint exists:

```powershell
az staticwebapp appsettings set `
  --name swa-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --setting-names `
    CleanupAutomationKey="$cleanupAutomationKey" `
    AuditEventsRetentionDays=30 `
    DiscountRuntimeRetentionHours=24
```

### Bicep Scheduler Resource

`main.bicep` can create an optional Logic App Consumption workflow that calls
the maintenance cleanup endpoint. It is disabled by default:

```json
"deployCleanupScheduler": {
  "value": false
}
```

Keep it disabled until:

1. `POST /api/system/maintenance/cleanup` is deployed.
2. `CleanupAutomationKey`, `AuditEventsRetentionDays`, and
   `DiscountRuntimeRetentionHours` exist in Static Web Apps app settings.
3. A manual call to the maintenance endpoint has been smoke-tested.

The current development Static Web Apps default hostname is documented in
`docs/architecture/azure-environments.md` as
`black-pond-085834203.7.azurestaticapps.net`. Confirm it before deploying a
scheduled caller:

```powershell
az staticwebapp show `
  --name swa-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --query "defaultHostname" `
  -o tsv
```

When ready, run `what-if` with explicit secure parameters:

```powershell
az deployment group what-if `
  --resource-group rg-simple-discount-verifier `
  --template-file infra/main.bicep `
  --parameters "@infra/parameters/develop.example.json" `
  --parameters `
    deployCleanupScheduler=true `
    cleanupAutomationKey="$cleanupAutomationKey" `
    cleanupEndpointUrl="https://black-pond-085834203.7.azurestaticapps.net/api/system/maintenance/cleanup"
```

Apply after reviewing the diff:

```powershell
az deployment group create `
  --resource-group rg-simple-discount-verifier `
  --template-file infra/main.bicep `
  --parameters "@infra/parameters/develop.example.json" `
  --parameters `
    deployCleanupScheduler=true `
    cleanupAutomationKey="$cleanupAutomationKey" `
    cleanupEndpointUrl="https://black-pond-085834203.7.azurestaticapps.net/api/system/maintenance/cleanup"
```

### Portal Verification

After the Logic App is created:

1. Open the Logic App resource, for example `la-sdv-cleanup-develop`.
2. Confirm the workflow is enabled only after the backend endpoint is ready.
3. Open the HTTP action and verify the target URL points to the correct Static
   Web Apps environment.
4. Confirm the HTTP action uses header `x-cleanup-key`.
5. Confirm secure inputs and secure outputs are enabled for the HTTP action so
   the key does not appear in run history.
6. Run the trigger manually once.
7. Verify the run history returns success from the cleanup endpoint.
8. Check Application Insights for the backend cleanup request and confirm logs
   contain only aggregate cleanup counts, not secrets or customer payload.

## Apply To The Current Development Environment

Use these commands from the repository root after signing in with Azure CLI.

```powershell
az account set --subscription 33427c73-b710-48a3-99a2-82217072bd94
```

Preview the deployment:

```powershell
az deployment group what-if `
  --resource-group rg-simple-discount-verifier `
  --template-file infra/main.bicep `
  --parameters "@infra/parameters/develop.example.json"
```

Apply the deployment:

```powershell
az deployment group create `
  --resource-group rg-simple-discount-verifier `
  --template-file infra/main.bicep `
  --parameters "@infra/parameters/develop.example.json"
```

Verify tables:

```powershell
az storage table list `
  --account-name sdvstorageaccount `
  --auth-mode login `
  --query "[].name" `
  --output table
```

Verify Application Insights:

```powershell
az monitor app-insights component show `
  --app ins-simple-discount-verifier `
  --resource-group rg-simple-discount-verifier `
  --query "{name:name, retentionInDays:retentionInDays, dailyDataCapInGB:dailyDataCapInGB}" `
  --output json
```

## Deploy A Tenant Production Environment

Tenant production is deployed from `release/<tenant>` into that tenant's own
Azure account or subscription. `master` is only the stable code baseline.

Use the complete human-and-agent stepper:

- `docs/process/deploy-azure-tenant.md`;
- `$deploy-azure-tenant` when running through Codex.

The generator is intentionally local-only and writes no secrets:

```powershell
.\infra\scripts\New-TenantDeploymentFiles.ps1 `
  -TenantSlug <lowercase-latin-slug> `
  -ReleaseBranch release/<lowercase-latin-slug> `
  -Location eastus2
```

Do not copy the old single-`master` production commands. The stepper creates an
empty SWA without GitHub integration, reviews Bicep `what-if`, applies one
deployment at a time, provisions runtime settings from local secret variables,
and enables cleanup only after endpoint smoke testing.
