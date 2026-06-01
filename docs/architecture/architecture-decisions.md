# Architecture Decisions

## Accepted for initial version

- Frontend: Angular + Ionic.
- Frontend runtime baseline: Node.js 24 LTS with npm 11.
- Frontend framework baseline: Angular 21 and Ionic Angular 8.
- Frontend rendering: client-side web application; Angular SSR is not used.
- Frontend platform target: web-only. Capacitor and native Android/iOS builds
  are out of scope unless a future requirement explicitly adds native mobile
  distribution.
- Angular change detection: zone-based Angular is used for the initial Ionic
  implementation, but application code must be written so it can move toward a
  zoneless setup. Prefer signals, explicit reactive state, Angular event
  bindings, and Angular services over zone-dependent implicit state updates.
- Frontend architecture: Feature-Sliced Design with `app`, `pages`, `widgets`,
  `features`, `entities`, and `shared` layers.
- Angular-first implementation rule: when Angular provides an appropriate tool,
  use it before browser-level or third-party alternatives. Examples:
  `HttpClient` over `fetch`, Angular forms/signals over ad hoc mutable form
  state, Angular router over manual navigation, and Angular DI services over
  module-level singletons.
- Reactive programming rule: frontend async workflows should use Observables,
  signals, and Angular reactive primitives. Avoid `async`/`await` in Angular
  application code unless an API cannot be represented cleanly through Angular
  or RxJS.
- Form state rule: use typed, signal-based or reactive Angular forms for
  non-trivial forms. Temporary form state should live in Angular state unless a
  requirement explicitly calls for browser storage.
- Frontend usage context: mobile-first; desktop support is secondary and must
  not compromise the primary mobile experience.
- Frontend interface language: Ukrainian.
- Frontend theme support: system, light, and dark modes.
- Barcode generation for scanner compatibility testing: `@bwip-js/browser`.
- Frontend quality gates: ESLint and Prettier are required.
- Frontend accessibility target: WCAG AA.
- Backend: Azure Functions.
- Storage: Azure Table Storage.
- Secrets: Azure Key Vault.
- Hosting: Azure.
- SMS: external SMS provider likely, SMS-Fly is a candidate.
- Main restaurant application does not expose public API.
- Main restaurant application can act as a REST client.
- Discount calculation is owned by the main restaurant application.
- Web service does not calculate discount amount.
- One-time barcode is invalidated immediately after successful validation.
- Customer profile has no complex workflow state.
- SMS verification during profile creation is out of scope for initial release.
