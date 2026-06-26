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
- Frontend application structure: the public customer redemption flow and the
  staff area are separated at the route-shell level. The public flow is the root
  route `/`; the staff area lives under `/admin`.
- Public redemption shell: the root route renders one mobile-first public page
  that owns a provider-scoped redemption flow store and uses Ionic `ion-nav` as
  an internal screen stack for the phone, SMS, and barcode steps. These steps do
  not get separate Angular routes.
- Public redemption screen loading: the phone-entry screen is part of the first
  route load. Later redemption screens and heavy rendering helpers are loaded
  dynamically when the flow reaches them.
- Public support QR: the public page shows a quiet always-available support
  icon action. Before a `correlationId` exists it explains that the support code
  is not ready. After a `correlationId` exists it opens a lazy-loaded dialog
  with a QR code and text value.
- Public support QR payload: the QR MUST encode the neutral text payload
  `SDV-SUPPORT:v1:<correlationId10>`, not an admin URL. The admin UI parses the
  payload and performs authorized inspection through `/api/backoffice/*`.
- Staff shell: `/admin` uses an Ionic top-level navigation shell with tabs for
  customer-profile work and service tools. A side menu is deferred until the
  staff area grows beyond the current top-level groups.
- Staff route groups: customer-profile work lives under `/admin/customers/*`;
  service tools live under `/admin/service/*`.
- Scanner survey placement: scanner compatibility testing is a temporary staff
  service tool and should move under `/admin/service/scanner-survey`. Any legacy
  `/scanner-survey*` route must remain protected while it exists and should be
  removed or redirected after migration.
- Angular-first implementation rule: when Angular provides an appropriate tool,
  use it before browser-level or third-party alternatives. Examples:
  `HttpClient` over `fetch`, Angular forms/signals over ad hoc mutable form
  state, Angular router over manual navigation, and Angular DI services over
  module-level singletons.
- Ionic layout rule: page-level responsive layout uses Ionic grid primitives
  (`ion-grid`, `ion-row`, `ion-col`) before custom CSS grid/flex containers.
  Content is placed inside `ion-col`, following Ionic's grid structure.
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
- Web barcode format: Code 128 with exactly 20 uppercase base32 characters and
  no prefix or separators.
- POS routing distinguishes barcode families by length: EAN13 is 13
  characters, ordinary card values are 18 characters, and the web discount
  verifier code is 20 characters.
- The web barcode value is `<phoneRuntimeKey10><correlationId10>`, containing
  the full runtime lookup key and the full support/audit correlation id.
- Frontend quality gates: ESLint and Prettier are required.
- Frontend accessibility target: WCAG AA.
- Hosting and API shell: Azure Static Web Apps.
- Frontend delivery: Angular SPA served by Azure Static Web Apps.
- API hosting for MVP: Azure Static Web Apps managed Azure Functions exposed
  under `/api`.
- CORS policy: avoid browser CORS complexity in the MVP by serving the Angular
  SPA and managed Functions API from the same Static Web Apps origin.
- Authentication for MVP: Azure Static Web Apps built-in authentication through
  `/.auth/*`.
- Static Web Apps auth endpoints used by the application:
  `/.auth/login/aad` starts the Microsoft Entra ID login flow,
  `/.auth/logout` ends the Static Web Apps session, and `/.auth/me` returns the
  current user.
- Static Web Apps built-in roles are `anonymous` and `authenticated`;
  `authenticated` means any user who successfully signs in through the provider.
- Administrative access MUST use the custom Static Web Apps role `admin`,
  because `authenticated` is too broad for the administrative area when using
  preconfigured Microsoft Entra ID.
- The `admin` role is a Static Web Apps custom role. It is not an Azure RBAC
  role and not a Microsoft Entra group.
- Administrative UI routes under `/admin*` and administrative API routes under
  `/api/backoffice/*` MUST allow only the `admin` role.
- For admin auth verification, `/scanner-survey*` and `/api/scanner-survey`
  require the custom Static Web Apps `admin` role while the home page remains
  public. The Angular `scanner-survey` route also uses `adminGuard`.
- External POS server-to-server routes under `/api/pos/*` MUST be reachable
  without Static Web Apps user authentication so the Azure Function can perform
  custom HMAC authentication internally.
- POS-facing Functions MUST validate HMAC authentication with headers
  `x-client-id`, `x-timestamp`, and `x-signature`.
- The initial POS client id is `main-pos-system`.
- POS HMAC secrets are server-side configuration values and MUST NOT be exposed
  to Angular or browser-delivered assets.
- POS request freshness MUST be checked with `x-timestamp`. A future phase will
  add a nonce, a used-nonce table, and periodic cleanup of that table.
- Public API routes under `/api/public/*` allow both `anonymous` and
  `authenticated` roles.
- Friendly authentication routes MAY be configured in `staticwebapp.config.json`
  with `/login` redirecting to `/.auth/login/aad` and `/logout` redirecting to
  `/.auth/logout`.
- Route protection for MVP: `staticwebapp.config.json`.
- Static Web Apps navigation fallback rewrites SPA routes to `/index.html` and
  excludes `/api/*`, `/.auth/*`, `/assets/*`, and static asset file extensions.
- Static Web Apps response overrides redirect `401` to
  `/.auth/login/aad?post_login_redirect_uri=.referrer` and rewrite `403` to
  `/forbidden.html`.
- Managed Functions runtime for Static Web Apps is `dotnet-isolated:8.0`.
- Admin role assignment for the small MVP is handled through Azure Portal:
  Static Web App -> Settings -> Role Management -> Invite, with provider `aad`,
  the user's email, domain, role `admin`, and an expiration.
- A Static Web Apps invitation does not create a Microsoft account; it links an
  already signed-in Microsoft identity to the `admin` role. The invitation email
  should match the identity the user will actually use to sign in.
- MVP exclusions: no separate Function App, no frontend MSAL setup, no dedicated
  App Registration for the Angular SPA, and no Managed Identity dependency.
- Observability for the production pilot is fixed in
  `docs/architecture/observability.md`.
- Application Insights is enabled only for the backend Azure Functions/API in
  the production pilot. Browser/frontend telemetry is out of scope.
- Business events remain in Azure Storage Table `AuditEvents`; Application
  Insights is used for operational telemetry only.
- Application Insights must use a 30-day retention period and a daily cap of
  100 MB/day to keep pilot monitoring cost close to the free allowance.
- Application Insights alerts are email-based for the production pilot.
- Backend telemetry must carry the same `correlationId` used by `AuditEvents`
  whenever a correlation id exists.
- Storage: Azure Storage Tables.
- Storage access: managed Azure Functions access Azure Storage Tables through a
  Storage connection string.
- MVP table design is fixed in `docs/architecture/table-storage-design.md`:
  customer profiles are keyed by normalized phone, runtime redemption data is
  stored as one current row per phone using an opaque phone runtime key, and
  audit events are stored separately by correlation id.
- Frontend storage rule: Angular must never access Azure Storage Tables
  directly; all storage operations go through `/api`.
- Secrets and connection strings: kept in Azure Static Web Apps/API
  configuration, never in Angular code or browser-delivered assets.
- SMS provider: SMS-Fly.
- Main restaurant application does not expose public API.
- Main restaurant application can act as a REST client.
- Discount calculation is owned by the main restaurant application.
- Web service does not calculate discount amount.
- Main application lookup key returned by the web service: phone number.
- When a customer requests a new one-time barcode while an active barcode exists,
  the web service recreates the barcode and overwrites the active table record.
- One-time barcode is invalidated immediately after successful validation.
- Customer profile has no complex workflow state.
- SMS verification during profile creation is out of scope for initial release.
