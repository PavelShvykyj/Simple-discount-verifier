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
| Current phase | PR-4 review |

## Task Tracker

| PR | ID | Task | Status | Notes | Evidence |
| --- | --- | --- | --- | --- | --- |
| PR-1 | BE-01 | Backend Foundation | done | Internal layers, response helpers, `IClock`, DI baseline added and delivered to Azure dev portal. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-20. Delivered to Azure dev portal. |
| PR-1 | BE-02 | Configuration And Secrets | done | Typed options for storage, POS, hashing, SMS, TTL and telemetry settings added and delivered to Azure dev portal. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-20. Delivered to Azure dev portal. |
| PR-2 | BE-03 | Domain Utilities | done | Phone normalization, JSON-backed questionnaire validation, ids, hashing, SMS code, barcode and POS HMAC helpers added and review feedback addressed. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-20. Review comments addressed in PR-2. |
| PR-3 | BE-04 | Storage Adapters | done | Azure Table repositories for `CustomerProfiles`, `DiscountRuntime`, and `AuditEvents` added with point reads/writes, continuation tokens, JSON serialization, conflict handling, and ETag-aware replace/delete operations. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-21. Merged to `develop` in PR-3. |
| PR-3 | BE-05 | Audit Writer And Telemetry Correlation | done | Shared audit writer added for append-only `AuditEvents`, phone hashing, metadata sanitization, and `correlationId` telemetry tagging. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-21. Merged to `develop` in PR-3. |
| PR-4 | BE-06 | Admin Customer Profile API | review | Admin create/list/get/update customer profile endpoints added with phone normalization, questionnaire validation, duplicate handling, ETag-safe updates and audit events. | `dotnet build functions\SimpleDiscountVerifier.Api.csproj` passed with 0 warnings, 0 errors on 2026-06-21. |
| PR-5 | BE-07 | Public Start Redemption API | todo | Start flow, profile lookup, SMS challenge and early audit chain. | |
| PR-5 | BE-08 | Public SMS Verification API | todo | Verify SMS, issue barcode, store only hash. | |
| PR-6 | BE-09 | POS Authentication | todo | HMAC validation for POS routes. | |
| PR-6 | BE-10 | POS Barcode Validation API | todo | One-time barcode validation, ETag consumption, idempotent replay. | |
| PR-7 | BE-11 | POS Customer Profile Lookup API | todo | On-demand POS profile lookup by phone. | |
| PR-7 | BE-12 | Admin Audit And Inspect API | todo | Audit list and read-only redemption inspect. | |
| PR-8 | BE-13 | System Health And Runtime Cleanup | todo | Health endpoint and expired runtime cleanup. | |
| PR-9 | BE-14 | Contract Verification And Manual Smoke Scenarios | todo | Manual integration scenarios for MVP API contract. | |
| PR-10 | BE-15 | Unit Tests | todo | Final dedicated unit-test task for domain/application rules. | |

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
