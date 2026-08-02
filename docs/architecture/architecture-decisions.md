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
- Angular lifecycle rule: do not use `ngDoCheck`. If a future implementation
  appears to need `ngDoCheck`, document the trade-off and ask the user for
  explicit confirmation before adding it.
- Frontend architecture: Feature-Sliced Design with `app`, `pages`, `widgets`,
  `features`, `entities`, and `shared` layers.
- Page design rule: before implementing a new page or materially changing an
  existing page, perform a mobile UX/UI best-practice review and a reusable
  component analysis.
- Reusable component rule: shared reusable UI components are dumb and
  independent by default. They receive data through signal-based inputs, expose
  user actions through signal-based outputs or projected content, use
  Ionic-first composition, and do not call APIs, navigate, read auth state,
  access stores, own business workflow, or depend on higher FSD layers.
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
- Angular template control flow rule: use modern Angular control flow blocks
  (`@if`, `@for`, `@switch`) instead of legacy `*ngIf`/`*ngFor`. The only
  allowed exception is a library directive without a block equivalent, such as
  Angular CDK virtual scroll's `*cdkVirtualFor`.
- Angular signal API rule: use signal-based component APIs (`input()`,
  `output()`, `model()`, `viewChild()`, `viewChildren()`, `contentChild()`,
  `contentChildren()`) instead of decorator APIs (`@Input`, `@Output`,
  `@ViewChild`, `@ViewChildren`, `@ContentChild`, `@ContentChildren`).
- Template binding rule: do not call component methods or arbitrary functions
  from templates to compute bound values. Expose derived display state,
  validation state, classes, labels, disabled flags, and similar values through
  `computed`, `linkedSignal`, Angular forms state, or equivalent reactive
  properties. Templates may read signals/computed signals and call event-handler
  commands for user actions.
- Ionic layout rule: page-level responsive layout uses Ionic grid primitives
  (`ion-grid`, `ion-row`, `ion-col`) before custom CSS grid/flex containers.
  Content is placed inside `ion-col`, following Ionic's grid structure.
- Ionic-first UI rule: new pages and page sections use Ionic components, Ionic
  CSS utility classes, Ionic CSS variables, and existing application CSS
  variables before custom CSS. New custom CSS classes, custom layout primitives,
  or app-specific CSS variables require prior confirmation with the need,
  Ionic alternative considered, trade-off, and verification plan documented.
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
- New or changed frontend pages must be checked in both light and dark theme
  modes for contrast, focus visibility, labels, validation messages, touch
  targets, loading/error states, and non-color-only state communication.
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
- Customer profiles contain required top-level system fields `phone` and
  `physicalCardNumber`; `physicalCardNumber` is a valid EAN-13 string and is not
  stored in questionnaire `answers[]`.
- The backend does not enforce uniqueness of `physicalCardNumber`; no auxiliary
  Azure Table entity, index table, or new API route is introduced for it.
- Administrators may change `physicalCardNumber`. POS does not synchronize it
  automatically, but may explicitly repeat the existing customer-profile
  lookup and update local `КодКарты` and `РучнойКод`.
- When a customer requests a new one-time barcode while an active barcode exists,
  the web service recreates the barcode and overwrites the active table record.
- One-time barcode is invalidated immediately after successful validation.
- Customer profile has no complex workflow state.
- Customer profile creation uses an operational phone-confirmation guard in the
  admin frontend. The frontend generates one two-digit code per entered phone,
  sends only `{ phone, code }` through the admin-only
  `/api/backoffice/customer-profiles/activation-code-sms` endpoint, and permits
  profile creation only after a local match.
- The activation endpoint is stateless: it validates the phone and code, sends
  the SMS through the existing provider, and stores no challenge or verification
  result. Changing the phone clears the frontend confirmation state; edit mode
  does not require confirmation.

## Public redemption hardening: phone enumeration and SMS bombing

- `POST /api/public/redemptions` returns a uniform `200 OK` regardless of
  whether a customer profile exists for the entered phone. There is no
  distinct "profile not found" HTTP response on this endpoint; see
  `docs/architecture/api-contract.md` for the full behavior.
- SMS send rate limiting is keyed by the deterministic phone runtime key
  (derived from the normalized phone), not by client IP address. IP address
  was rejected as the primary rate-limit key because this app's primary usage
  scenario is a customer connecting to a restaurant/venue's shared Wi-Fi (all
  patrons share one NAT/public IP) or to a mobile carrier using carrier-grade
  NAT (many unrelated subscribers share one public IP), both of which make
  IP-based limits either falsely block unrelated customers or fail to isolate
  a single abusive client. IP MAY still be logged as a secondary monitoring
  signal but must not gate legitimate requests.
- SMS send rate limits: minimum 180 seconds between sends, maximum 5 sends per
  fixed one-hour window, maximum 10 sends per fixed 24-hour window, all per phone runtime key.
  Configured via the `SmsRetryAfterSeconds`, `SmsMaxPerHour`, and
  `SmsMaxPerDay` app settings.
- The rate limit is enforced with an atomic optimistic-concurrency
  reserve-then-act pattern on the `DiscountRuntime` row (insert-if-absent or
  ETag-conditional replace with a bounded retry loop) so a burst of parallel
  requests for the same phone cannot bypass the limit through a
  check-then-act race condition.
- CAPTCHA/bot-challenge on this endpoint was initially deferred; it is now
  covered by invisible Cloudflare Turnstile (see the dedicated section below).
  Mass cross-number enumeration by an operator running a real browser/headless
  Turnstile-capable client, each request individually under the rate limit,
  remains a known, accepted residual gap.
- SMS-provider rejection is audit-only and returns the same generic `200 OK` as
  the registered-success and profile-not-found paths. A configurable response
  floor masks normal SMS-provider latency without introducing a queue.

## Invisible Cloudflare Turnstile with a config kill switch

- `POST /api/public/redemptions` verifies an optional `turnstileToken`
  (produced by an invisible Cloudflare Turnstile widget on the frontend)
  against Cloudflare's `siteverify` endpoint. This runs before phone
  normalization/profile lookup so a failed check never leaks profile
  existence, and reuses the enumeration-safe design established above.
- The check is fully gated by a single backend app setting,
  `TurnstileEnabled` (bound to `TurnstileOptions.Enabled`). When `false`
  (the default), `CloudflareTurnstileVerifier.VerifyAsync` returns `true`
  immediately with **no network call** and without reading `TurnstileSecretKey`
  at all — the endpoint behaves exactly as it did before Turnstile existed.
  This is a deliberate, explicit operator-controlled kill switch: if Cloudflare
  Turnstile itself has an outage or starts misbehaving, an operator flips
  `TurnstileEnabled=false` via `az staticwebapp appsettings set` (see
  `infra/README.md`) and the redemption flow is immediately restored for
  legitimate customers, with no redeploy.
- While `TurnstileEnabled=true`, verification is intentionally **fail-closed**:
  a missing/oversized/invalid token, missing Turnstile settings, a non-success
  Cloudflare response, or a transport failure/timeout talking to Cloudflare all
  result in the request being rejected (`400 turnstile_verification_failed`).
  Automatically falling back to "allow" on any verification error was
  considered and rejected: an attacker could trivially trigger transport
  errors (e.g. sending garbage tokens, or exhausting the shared `HttpClient`)
  to auto-disable the very check meant to stop them. The kill switch is
  therefore a manual, operator-initiated action tied to a real incident, not an
  automatic behavior triggered by request-level failures. Siteverify calls
  have a 10-second application timeout; successful responses must match the
  `start_redemption` action and configured hostname.
- `GET /api/public/redemptions/config` supplies the enabled flag and public site
  key at runtime. The frontend blocks start/resend while Turnstile is required
  but a one-time token is not ready. A deployment can therefore enable or
  disable Turnstile without rebuilding the static frontend.
- The frontend widget uses Turnstile's default `execution: 'render'` mode
  (auto-runs once mounted, invisible, no visible UI in the normal case) rather
  than `execution: 'execute'` bound to the submit button, to avoid adding
  latency at the moment the customer taps submit. Tokens are single-use
  server-side, so the widget is reset (a fresh token requested) after every
  `startRedemption`/`resendSms` attempt, successful or not.
- Requests rejected by Turnstile are not persisted to `AuditEvents`; this
  avoids one storage write per anonymous bot request. The generated
  `correlationId` is still returned in the error response.
