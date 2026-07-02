# Frontend Implementation Status

Дата создания: 2026-06-30

Этот документ отслеживает выполнение frontend PR-плана из
`docs/architecture/frontend-application-structure.md`.

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
| Frontend baseline | Angular 21, Ionic Angular 8, client-side web app |
| Current branch | `codex-frontend-prs` |
| Current phase | PR-5 next |
| Source plan | `docs/architecture/frontend-application-structure.md` |

## Task Tracker

| PR | Task | Status | Notes | Evidence |
| --- | --- | --- | --- | --- |
| PR-1 | Frontend Shell And Routing Baseline | done | `/admin` shell, lazy admin route groups, theme-mode selector placement, scanner-survey migration path, protected route config, and local API proxy support are in place. | Implementation present under `frontend/src/app`, `frontend/src/pages/admin-*`, `frontend/src/pages/scanner-survey`, `frontend/src/shared/theme`, `frontend/public/staticwebapp.config.json`, and `frontend/proxy.conf.json`. `npm --prefix frontend run lint` and `npm --prefix frontend run build` passed on 2026-06-30; build kept the existing initial bundle budget warning. |
| PR-2 | Public Redemption Flow Skeleton | done | Temporary home route replaced by `PublicRedemptionPage`; provider-scoped redemption flow store, `ion-nav` phone/SMS/barcode screen stack, typed public API client contracts, loading states, retry state, and error surfaces are in place without barcode rendering. | Implementation present under `frontend/src/pages/public-redemption` and `frontend/src/features/redemption-flow`; store tests present in `frontend/src/features/redemption-flow/model/redemption-flow.store.spec.ts`. `npm --prefix frontend run lint` and `npm --prefix frontend run build` passed on 2026-06-30; build kept the existing initial bundle budget warning. |
| PR-3 | Public Barcode And Support QR | done | Dynamic barcode rendering, quiet support action, pre-correlation explanation, lazy support QR dialog, and visible plain `correlationId` are implemented. | `npm --prefix frontend run lint` and `npm --prefix frontend run build` passed on 2026-06-30; build kept the existing initial bundle budget warning. User review accepted on 2026-07-01. |
| PR-4 | Admin Customer Profiles | done | Staff customer profile list, exact phone lookup, customer profile field definitions, create/edit form flow, and `/api/backoffice/customer-profiles` integration are complete for the PR-4 scope. | Committed as `d47d604 PR-4-Edit-form`. |
| PR-5 | Admin Service Inspect And Health | todo | Implement service hub, redemption inspect, support QR/barcode scanner, and system health view. | |
| PR-6 | Performance And Mobile UX Hardening | todo | Verify production bundle, mobile UX, route chunk boundaries, browser back behavior, and accessibility. | |

## Update Rules

- Move one PR-sized task at a time from `todo` to `in-progress`.
- Add evidence when status changes to `review` or `done`: command, test,
  build, visual check, PR/commit, or file reference.
- Keep PR status aligned with the PR plan in
  `docs/architecture/frontend-application-structure.md`.
- Do not mark a frontend PR `done` unless the implemented route behavior,
  mobile-first states, lazy-loading boundaries, accessibility expectations, and
  documented verification match the plan for that PR.
