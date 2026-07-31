# Backend API Implementation Readiness

This document lists what is still needed before implementing the documented
MVP APIs in `docs/architecture/api-contract.md`.

## Current Backend State

Implemented:

- Azure Functions isolated worker project under `functions`.
- `POST /api/scanner-survey`.
- Azure Table writer for `ScannerSurveyResults`.
- Static Web Apps app settings:
  - `APPLICATIONINSIGHTS_CONNECTION_STRING`;
  - `AppStorageConnectionString`;
  - `ScannerSurveyTableName`.
- `functions/local.settings.json.example` includes planned local configuration
  keys for the documented MVP APIs.

Not implemented yet:

- Public redemption APIs.
- Admin customer profile APIs.
- Admin redemption inspection API.
- POS barcode validation API.
- POS customer profile lookup API.
- Admin audit-events API.
- System health API.
- SMS-Fly integration.
- POS HMAC authentication.
- Application Insights instrumentation in function code beyond hosting-level
  configuration.

## Infrastructure Prerequisites

The backend needs these Azure Tables before API work can start cleanly:

- `CustomerProfiles`.
- `DiscountRuntime`.
- `AuditEvents`.

`infra/main.bicep` now declares these tables along with the existing
`ScannerSurveyResults` table.

The backend should also receive table names through configuration, not hardcode
them:

- `ScannerSurveyTableName`.
- `CustomerProfilesTableName`.
- `DiscountRuntimeTableName`.
- `AuditEventsTableName`.

These three new table-name settings are present in
`functions/local.settings.json.example`, but they still need to be added to the
Azure Static Web Apps application settings before deployed Functions can use
them.

## Required Configuration Names

Storage:

- `AppStorageConnectionString`.
- `ScannerSurveyTableName`.
- `CustomerProfilesTableName`.
- `DiscountRuntimeTableName`.
- `AuditEventsTableName`.

POS authentication:

- `PosMainClientId` with MVP value `main-pos-system`.
- `PosMainClientHmacSecret`.
- Request freshness tolerance setting, for example
  `PosRequestFreshnessToleranceSeconds`.

Hashing and token generation:

- `PhoneRuntimeKeySecret`.
- `SmsCodeHashSecret`.
- `BarcodeHashSecret`.
- `AuditPhoneHashSecret`.

SMS:

- `SmsFlyApiKey`.
- `SmsFlySender`.
- SMS expiration setting, for example `SmsCodeTtlSeconds`.
- SMS retry throttle setting, for example `SmsRetryAfterSeconds`.

Barcode/runtime:

- `BarcodeTtlSeconds`.
- Runtime cleanup retention setting, for example
  `DiscountRuntimeRetentionHours`.

Observability:

- `APPLICATIONINSIGHTS_CONNECTION_STRING`.

## Code Building Blocks To Add

Storage:

- Typed table clients for `CustomerProfiles`, `DiscountRuntime`, and
  `AuditEvents`.
- A shared table configuration/options object.
- ETag-aware update helpers for one-time barcode consumption.
- Continuation-token support for list endpoints.

Domain services:

- Ukrainian phone normalization and validation.
- Questionnaire definition and answer validation.
- Correlation id generation.
- Phone hash generation.
- Phone runtime key generation.
- SMS code generation and hashing.
- Barcode value generation and hashing.
- Audit event writer.
- POS HMAC validator.
- SMS-Fly client abstraction.
- Clock abstraction for TTL and tests.

Functions:

- `POST /api/public/redemptions`.
- `POST /api/public/redemptions/{redemptionKey}/sms-verifications`.
- `POST /api/backoffice/customer-profiles`.
- `GET /api/backoffice/customer-profiles`.
- `GET /api/backoffice/customer-profiles/by-phone/{phone}`.
- `PATCH /api/backoffice/customer-profiles/by-phone/{phone}`.
- `POST /api/backoffice/redemptions/inspect`.
- `POST /api/pos/barcodes/validate`.
- `POST /api/pos/customer-profiles/lookup`.
- `GET /api/backoffice/audit-events`.
- `GET /api/system/health`.

Testing:

- Unit tests for phone normalization.
- Unit tests for questionnaire validation.
- Unit tests for HMAC canonical-string verification.
- Unit tests for barcode parsing.
- Unit tests for idempotent POS retry behavior.
- Storage integration tests or emulator-backed tests for table operations.
- Function-level tests for request and response contracts.

## Observability Work Before Alerts Are Useful

The Azure resources can create alert rules, but alerts become meaningful only
after backend code emits the expected telemetry:

- request telemetry for each function;
- exception telemetry;
- dependency telemetry for Azure Table operations;
- dependency telemetry for SMS-Fly calls;
- traces that include `correlationId` when available;
- custom metrics if needed for SMS-specific or business-adjacent operational
  failures.

The health alert also needs a real `GET /api/system/health` endpoint and a
chosen availability-test mechanism.

## Suggested Implementation Order

1. Apply infrastructure so the missing tables exist.
2. Add missing Azure Static Web Apps application settings for the documented
   MVP APIs.
3. Add backend options/configuration binding.
4. Add shared domain utilities and tests.
5. Add storage clients and audit writer.
6. Implement admin customer profile APIs first, because public redemption
   depends on saved profiles.
7. Implement public redemption and SMS verification.
8. Implement POS HMAC and barcode validation.
9. Implement POS profile lookup.
10. Implement admin inspection and audit APIs.
11. Implement health endpoint.
12. Enable scheduled query alerts after telemetry is verified.
