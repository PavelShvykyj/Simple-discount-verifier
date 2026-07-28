# Backend API Implementation Status

Дата создания: 2026-06-20

Этот документ отслеживает выполнение задач из
`docs/architecture/backend-api-implementation-plan.md`.

Git/GitHub стратегия выполнения задач зафиксирована в
`docs/architecture/backend-api-git-pr-strategy.md`.

## Статусы

| Status | Meaning |
| --- | --- |
| `todo` | Задача еще не начата. |
| `in-progress` | Задача сейчас реализуется. |
| `blocked` | Есть внешний блокер или требуется решение. |
| `review` | Реализация готова, нужна проверка. |
| `done` | Реализация завершена и проверена. |

## Общий Статус

| Field | Value |
| --- | --- |
| Backend baseline | Azure Static Web Apps managed Functions, .NET isolated 8.0 |
| Existing implemented API | `POST /api/scanner-survey` |
| Main storage | Azure Table Storage |
| Required tables | `CustomerProfiles`, `DiscountRuntime`, `AuditEvents`, `ScannerSurveyResults` |
| Current phase | PR-8 review |

## Task Tracker

| PR | ID | Task | Status | Notes | Evidence |
| --- | --- | --- | --- | --- | --- |
| PR-1 | BE-01 | Backend Foundation | done | Internal layers, response helpers, `IClock`, DI baseline added and delivered to Azure dev portal. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-20. Delivered to Azure dev portal. |
| PR-1 | BE-02 | Configuration And Secrets | done | Typed options for storage, POS, hashing, SMS, TTL and telemetry settings added and delivered to Azure dev portal. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-20. Delivered to Azure dev portal. |
| PR-2 | BE-03 | Domain Utilities | done | Phone normalization, JSON-backed questionnaire validation, ids, hashing, SMS code, barcode and POS HMAC helpers added and review feedback addressed. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-20. Review comments addressed in PR-2. |
| PR-3 | BE-04 | Storage Adapters | done | Azure Table repositories for `CustomerProfiles`, `DiscountRuntime`, and `AuditEvents` added with point reads/writes, continuation tokens, JSON serialization, conflict handling, and ETag-aware replace/delete operations. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-21. Merged to `develop` in PR-3. |
| PR-3 | BE-05 | Audit Writer And Telemetry Correlation | done | Shared audit writer added for append-only `AuditEvents`, phone hashing, metadata sanitization, and `correlationId` telemetry tagging. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-21. Merged to `develop` in PR-3. |
| PR-4 | BE-06 | Admin Customer Profile API | done | Admin create/list/get/update customer profile endpoints added with phone normalization, questionnaire validation, duplicate handling, ETag-safe updates and audit events. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-21. Merged to `develop` in PR-4. |
| PR-5 | BE-07 | Public Start Redemption API | done | Public start endpoint added with early `correlationId`, audit chain, profile lookup, SMS retry throttle, SMS-Fly sender abstraction, runtime upsert after profile match and documented error responses. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-22. PR-5 marked complete on 2026-06-23. |
| PR-5 | BE-08 | Public SMS Verification API | done | SMS verification endpoint added with runtime lookup, expiry/max-attempt checks, failed-attempt persistence, barcode generation, `BarcodeHash` storage and audit events for verification/barcode issuance. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-22. PR-5 marked complete on 2026-06-23. |
| PR-6 | BE-09 | POS Authentication | done | Reusable POS HMAC authentication service added for `x-client-id`, `x-timestamp`, `x-signature`, configured client id/secret and freshness tolerance. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-23. Merged to `develop` in PR-6. |
| PR-6 | BE-10 | POS Barcode Validation API | done | `POST /api/pos/barcodes/validate` added with request validation, barcode parsing, runtime lookup, hash/TTL checks, ETag-based consumption, idempotent replay for the same `scanId`, `already_used` for a different `scanId`, and POS audit events. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-23. Merged to `develop` in PR-6. |
| PR-7 | BE-11 | POS Customer Profile Lookup API | done | `POST /api/pos/customer-profiles/lookup` added with POS HMAC authentication, phone normalization, profile point lookup, POS audit events, success response and `profile_not_found` handling. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-23. Merged to `develop` in PR-7. |
| PR-7 | BE-12 | Admin Audit And Inspect API | done | `GET /api/backoffice/audit-events` and `POST /api/backoffice/redemptions/inspect` added with audit filtering, pagination, barcode/correlation lookup, read-only status reconstruction, runtime/profile enrichment and inspect response models. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-23. Merged to `develop` in PR-7. |
| PR-8 | BE-13 | System Health And Runtime Cleanup | review | Health endpoint, admin/maintenance cleanup endpoints, bounded cleanup for `DiscountRuntime` and `AuditEvents`, SWA route rules, IaC settings, and optional Logic App scheduler support added. | `az bicep build --file infra\main.bicep` passed on 2026-06-24 with existing metadata warnings only. `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-24. |
| PR-9 | BE-14 | Contract Verification And Manual Smoke Scenarios | todo | Manual integration scenarios for MVP API contract. | |
| PR-10 | BE-15 | Unit Tests | todo | Final dedicated unit-test task for domain/application rules. | |
| Feature | BE-16 | Admin Profile Phone Confirmation SMS | review | Admin-only stateless activation SMS endpoint validates normalized phone and exactly two ASCII digits, reuses `ISmsSender`, returns empty `202`, stores no challenge, and maps provider transport failures to `502`. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings and 0 errors on 2026-07-28. Local smoke returned the documented `400` codes for malformed JSON, invalid phone, and invalid code. Deployed SWA-role, accepted-send, and provider-failure smoke remain pending. |

## Open Decisions

| Decision | Status | Notes |
| --- | --- | --- |
| Choose smoke-test format | open | Recommended baseline: human-readable smoke scenarios document first, small smoke script later for HMAC and repeatable flows. |

## Accepted Decisions

| Decision | Accepted | Notes |
| --- | --- | --- |
| Keep single `functions` project with internal clean-architecture folders | 2026-06-20 | Keeps MVP simple and matches current repo shape. |
| Do not add local fake SMS provider before SMS-Fly integration | 2026-06-20 | Public redemption smoke checks require real SMS configuration and manual code entry. |
| Include runtime cleanup timer in first backend implementation pass | 2026-06-20 | Cleanup is part of the first backend MVP pass, not a later follow-up. |
| Use small feature-branch PRs for backend implementation | 2026-06-20 | Merge to `develop` deploys Azure dev, so each PR must be deploy-safe. |

## Update Rules

- Move one task at a time from `todo` to `in-progress`.
- Add evidence when status changes to `review` or `done`: command, test,
  endpoint call, PR/commit, or file reference.
- Keep blockers concrete and attach the decision or missing dependency.
- Do not mark API tasks `done` unless response shape, storage side effects and
  audit behavior match the documented contract.
