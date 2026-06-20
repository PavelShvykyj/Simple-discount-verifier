# Observability

This document fixes the production-pilot observability baseline.

## Scope

- Application Insights is enabled for backend Azure Functions/API telemetry.
- Frontend/browser telemetry is not enabled for the production pilot.
- Business events remain in Azure Storage Table `AuditEvents`.
- Application Insights is used for operational telemetry only.

## Telemetry Rules

- Send backend requests, exceptions, dependencies, traces, and custom metrics to
  Application Insights.
- Do not send business event payloads to Application Insights. Store those
  events in `AuditEvents`.
- Do not send raw request bodies, SMS codes, barcode values, raw phone numbers,
  or other sensitive customer data to Application Insights.
- Use hashed or opaque identifiers where technical diagnosis needs an identifier.
- Include the current `correlationId` in Application Insights telemetry whenever
  one exists, so operational telemetry can be matched with `AuditEvents`.
- Enable sampling for backend telemetry.

## Retention And Cost Controls

- Application Insights retention: 30 days.
- Daily cap: 100 MB/day.
- The 100 MB/day cap is chosen to keep the production pilot close to the Azure
  Monitor free monthly ingestion allowance when telemetry volume is normal.
- If the cap is reached, preserving cost control is preferred over collecting
  complete operational telemetry for that day.

## Alerts

Production-pilot alerts are sent by email.

Required alert coverage:

- unhandled backend exceptions;
- elevated HTTP 5xx rate;
- elevated backend request latency;
- SMS provider failures;
- Azure Table Storage dependency failures;
- availability/health endpoint failure.

POS validation failures are not an Application Insights alert for the production
pilot. Fraud and support analysis for those events belongs in `AuditEvents`.
