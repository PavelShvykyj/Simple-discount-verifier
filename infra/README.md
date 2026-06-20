# Azure Infrastructure

This folder contains the first infrastructure-as-code baseline for the Azure
resources described in `docs/architecture/azure-environments.md`.

## Files

- `main.bicep` creates or updates the Azure resources for one environment.
- `parameters/develop.example.json` matches the currently inspected
  development environment.
- `parameters/production.example.json` is a starting point for the future
  `master` production-pilot environment.

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

## What Stays Manual For Now

- Real secret values.
- Static Web Apps `admin` role invitations.
- GitHub Actions secrets.
- Custom domains and DNS validation.
- Availability/health alert wiring until `GET /api/system/health` exists and
  the desired availability-test mechanism is chosen.

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

For a brand-new environment, such as the future `master` production pilot,
`manageStaticWebAppResource=true` is appropriate for initial creation. After
the SWA is created and connected, normal drift-control deployments can set it
back to `false`.

When enabled, the template expects secure values for:

- `appStorageConnectionString`;
- `applicationInsightsConnectionString`;
- `mainPosSystemHmacSecret`;
- `phoneRuntimeKeySecret`;
- `smsCodeHashSecret`;
- `barcodeHashSecret`;
- `auditPhoneHashSecret`;
- `smsFlyApiKey`.

It also writes non-secret table-name settings:

- `ScannerSurveyTableName`;
- `CustomerProfilesTableName`;
- `DiscountRuntimeTableName`;
- `AuditEventsTableName`.

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
    SmsRetryAfterSeconds=5 `
    BarcodeTtlSeconds=180 `
    DiscountRuntimeRetentionHours=24
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
    SmsRetryAfterSeconds=5 `
    BarcodeTtlSeconds=180 `
    DiscountRuntimeRetentionHours=24 `
    PosMainClientHmacSecret=CHANGE_ME_MANUALLY `
    PhoneRuntimeKeySecret=CHANGE_ME_MANUALLY `
    SmsCodeHashSecret=CHANGE_ME_MANUALLY `
    BarcodeHashSecret=CHANGE_ME_MANUALLY `
    AuditPhoneHashSecret=CHANGE_ME_MANUALLY `
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
- `SmsFlyApiKey`;
- `SmsFlySender`, if the sender value is operationally sensitive.

### Secret App Settings

Set these on the Azure Static Web App under
**Settings -> Environment variables**, or with Azure CLI
`az staticwebapp appsettings set`.

| Setting | Purpose | Source |
| --- | --- | --- |
| `AppStorageConnectionString` | Lets managed Functions access Azure Table Storage. Already configured in `develop`; required in every environment. | Azure Storage Account access key connection string. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connects backend telemetry to Application Insights. Already configured in `develop`; required in every environment. | Application Insights connection string. |
| `PosMainClientHmacSecret` | Shared secret for validating POS HMAC requests from `main-pos-system`. | Generate a long random secret and provision the same value to the POS integration owner. |
| `PhoneRuntimeKeySecret` | Derives deterministic opaque phone runtime keys without exposing raw phone numbers. | Generate a long random backend-only secret per environment. |
| `SmsCodeHashSecret` | Hashes SMS codes so raw SMS codes are not stored. | Generate a long random backend-only secret per environment. |
| `BarcodeHashSecret` | Hashes barcode values so raw active barcodes are not stored. | Generate a long random backend-only secret per environment. |
| `AuditPhoneHashSecret` | Hashes phone values for `AuditEvents` diagnostics without storing raw phones. | Generate a long random backend-only secret per environment. |
| `SmsFlyApiKey` | Authenticates requests to SMS-Fly. | SMS-Fly account/API credentials. |
| `SmsFlySender` | Sender id/name used for SMS messages. | SMS-Fly-approved sender value. Treat as sensitive if the provider or operations policy requires it. |

Recommended random secret generation from PowerShell:

```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
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
  --parameters infra/parameters/develop.example.json
```

Apply the deployment:

```powershell
az deployment group create `
  --resource-group rg-simple-discount-verifier `
  --template-file infra/main.bicep `
  --parameters infra/parameters/develop.example.json
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

## Prepare Production Pilot

Production pilot is deployed from `master` into a separate Azure account or
subscription.

One-time preparation:

1. Sign in to the production Azure account.
2. Set the production subscription:

   ```powershell
   az account set --subscription <production-subscription-id>
   ```

3. Create or choose the production resource group:

   ```powershell
   az group create `
     --name <production-resource-group> `
     --location westeurope
   ```

4. Copy `parameters/production.example.json` to a local, uncommitted parameter
   file if real production names differ from the example.
5. Replace placeholder resource names and alert emails in that local parameter
   file.
6. Keep `manageStaticWebAppResource=true` for the first production deployment
   if Bicep should create the Static Web App.
7. Supply a secure `repositoryToken` when creating a brand-new SWA through
   Bicep, or create/connect the SWA through Azure Portal and then set
   `manageStaticWebAppResource=false` for later drift-control deployments.

Preview production:

```powershell
az deployment group what-if `
  --resource-group <production-resource-group> `
  --template-file infra/main.bicep `
  --parameters <local-production-parameters.json> `
  --parameters repositoryToken="<github-token>"
```

Apply production:

```powershell
az deployment group create `
  --resource-group <production-resource-group> `
  --template-file infra/main.bicep `
  --parameters <local-production-parameters.json> `
  --parameters repositoryToken="<github-token>"
```

After production infrastructure exists:

1. Add production Static Web Apps app settings, including real secrets, from a
   secure local source.
2. Invite production administrators to the Static Web Apps `admin` custom role.
3. Get the production Static Web Apps deployment token.
4. Add a production GitHub Actions secret, for example
   `AZURE_STATIC_WEB_APPS_API_TOKEN_PRODUCTION`.
5. Add a separate GitHub Actions workflow for `master`.
6. Run the first `master` deployment.
7. Verify the public site, protected admin routes, storage tables, Application
   Insights retention/cap, and telemetry.

After the first production SWA is created and connected, normal production
infrastructure deployments should usually set `manageStaticWebAppResource=false`
unless the task is intentionally changing SWA resource properties.
