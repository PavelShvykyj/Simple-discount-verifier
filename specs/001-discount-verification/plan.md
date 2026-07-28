# Implementation Plan: Client-Side Phone Confirmation During Profile Creation

**Branch**: `physical-card-number-implementation` | **Date**: 2026-07-28 | **Spec**: `specs/001-discount-verification/spec.md`

**Input**: Existing feature specification plus the confirmed 2026-07-28
clarification: during customer-profile creation the frontend generates and
locally compares a two-digit SMS code; the backend only validates the send
request and relays the SMS without storing verification state.

## Summary

Add a lightweight phone-entry confirmation step to the administrator's
customer-profile create form:

1. The frontend generates a random two-character decimal code from `00` through
   `99` after the phone field is valid.
2. The frontend sends only normalized `phone` and `code` to a new admin-only
   backend endpoint.
3. The backend validates both values and sends the code through the existing
   `ISmsSender`/SMS-Fly integration without storing a challenge, verification
   result, or profile draft. Success is `202 Accepted` with no response body.
4. The customer dictates the received code to the administrator.
5. The frontend compares the entered value with its in-memory value and allows
   the existing full profile create request only after an exact match.

This is an operational guard against an incorrectly entered phone, not a
server-attested authentication mechanism. The agreed trust model treats the
administrator as trusted. Profile editing, persisted verification status,
Azure Table changes, and POS changes are outside this implementation.

**Mandatory authorization requirement**: the new endpoint must remain under
`/api/backoffice/*` and must be available only to an authenticated identity
with the Azure Static Web Apps custom role `admin`. Anonymous users and
authenticated users without `admin` must be rejected by Static Web Apps before
the Function is invoked.

## Technical Context

**Language/Version**: TypeScript 5.9 with Angular 21.2; C# on .NET 8 with Azure
Functions v4

**Primary Dependencies**: Ionic Angular 8.8.8, RxJS 7.8, existing
`ISmsSender` and `SmsFlyClient`; no new dependency

**Storage**: None added. The activation code and match state live only in the
Angular component instance; the backend performs no storage read or write for
this flow.

**Testing**: Vitest through `ng test` for frontend state and API calls;
`dotnet build` plus a bounded local/manual HTTP smoke check for the new
Function because the repository has no backend unit-test project yet

**Target Platform**: Mobile-first administrator web UI hosted by Azure Static
Web Apps with a same-origin managed Azure Function

**Project Type**: Angular/Ionic frontend plus .NET Azure Functions backend

**Performance Goals**: One small SMS-send request before the existing profile
create request; no polling, extra profile payload transfer, or storage I/O

**Constraints**:

- The SMS-send call contains only `phone` and two-character `code`.
- The expected code is not stored in local storage, session storage, a service,
  Azure Table Storage, audit data, or logs.
- Changing the phone, resetting the form, closing it, or reloading the page
  clears the confirmation.
- The frontend generates one code per phone/form instance and reuses that code
  for retry/resend, avoiding delayed-SMS and ambiguous-response races.
- The existing profile create request and persisted `CustomerProfile` shape do
  not change.
- The confirmation UI and requirement are create-mode only; edit mode retains
  its current behavior.
- The endpoint path must match the existing `/api/backoffice/*` Static Web Apps
  rule with `allowedRoles: ["admin"]`; it must not be exposed under a public or
  merely authenticated route.

**Scale/Scope**: One existing form, one existing Angular API client, one new
backoffice endpoint, and the existing SMS provider integration

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- **Mobile-first UX — PASS**: the phone send action and two-digit input are
  placed directly below the existing phone input with no horizontal layout or
  modal sub-flow.
- **Page design review — PASS**: the existing create page is materially changed;
  the design below covers primary action order, touch targets, numeric keyboard,
  loading/error/success states, and one-handed use.
- **Technology stack — PASS**: Angular, Ionic, RxJS, .NET, and the existing SMS
  integration are reused; no framework or package is added.
- **Angular modern syntax — PASS**: conditional code-entry/status blocks use
  `@if`.
- **Angular signal APIs — PASS**: local send/match state uses signals and
  computed state.
- **Template bindings — PASS**: derived enablement, match, and error state are
  exposed through signals/computed values rather than template method calls.
- **Ionic-first UI — PASS**: reuse the existing `ion-input-otp` pattern plus
  `ion-button`, `ion-spinner`, `ion-note`, and Ionic utilities. No new custom
  CSS or app variable is planned.
- **Feature-Sliced Design — PASS**: the flow stays in the existing
  `features/customer-profile-form`; the HTTP method stays in the existing
  customer-profile entity API.
- **Reusable component analysis — PASS**: the two-digit field is specific to
  profile creation and does not justify a new reusable component. Existing
  shared toast handling is reused.
- **Flow separation — PASS**: no code or state is shared with the public
  redemption SMS-verification flow.
- **Quality gates — PASS**: targeted tests, full frontend tests, lint, format
  check, frontend build, and backend build are completion checks.
- **Accessibility — PASS**: labelled numeric input, visible instructions,
  busy/disabled state, explicit mismatch and success text, keyboard access,
  touch-sized Ionic buttons, and light/dark review are included.

### Page Design Review

- **Primary task order**: enter phone → send code → enter dictated code → finish
  the questionnaire → create profile.
- **One-handed use**: send and create actions remain full-width Ionic buttons;
  no side-by-side controls are required.
- **Input ergonomics**: reuse Ionic `ion-input-otp` with `type="text"`,
  `inputmode="numeric"`, and `length="2"` so `00` is preserved and mobile
  shows the numeric keyboard.
- **Navigation**: no new route, modal, or nested flow is introduced.
- **Loading**: the send button is disabled and shows a spinner only while its
  request is in flight.
- **Error**: invalid phone stays under the phone field; provider/request failure
  uses the shared error toast; a two-digit mismatch is shown next to the code
  field without revealing the expected code.
- **Success**: visible text states that the phone is confirmed; success does not
  rely on color alone.
- **Reset behavior**: any phone edit clears the sent/matched state even if the
  administrator later types the old phone again.
- **Readability**: short Ukrainian labels and helper text are used; no
  horizontal scrolling or custom layout is needed.

## Project Structure

### Documentation (this feature)

```text
specs/001-discount-verification/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── admin-profile-activation-sms.openapi.yaml
└── tasks.md
```

`tasks.md` is not created by this planning step.

### Source Code (repository root)

```text
functions/
├── Application/
│   └── CustomerProfiles/
│       ├── AdminCustomerProfileService.cs
│       └── CustomerProfileErrorCodes.cs
├── Contracts/
│   └── Admin/
│       └── CustomerProfileRequest.cs
└── Functions/
    └── Admin/
        ├── AdminApiRoutes.cs
        └── AdminCustomerProfilesFunction.cs
functions/Infrastructure/Sms/
└── SmsFlyClient.cs

frontend/src/
├── entities/
│   └── customer-profile/
│       ├── api/
│       │   ├── admin-customer-profiles.api.ts
│       │   └── admin-customer-profiles.api.spec.ts
│       └── model/
│           └── customer-profile.types.ts
└── features/
    └── customer-profile-form/
        └── ui/
            ├── customer-profile-form.component.ts
            ├── customer-profile-form.component.html
            └── customer-profile-form.component.spec.ts
```

Documentation to align during implementation:

```text
specs/001-discount-verification/spec.md
docs/product/discount-verification-summary.md
docs/architecture/api-contract.md
docs/architecture/data-lifecycle.md
docs/architecture/table-storage-design.md
docs/architecture/backend-api-implementation-status.md
docs/architecture/frontend-implementation-status.md
```

**Structure Decision**: Extend the existing profile application service,
Function class, entity API, and feature form. Do not add a verification service,
state store, shared UI component, backend repository, table, Azure resource, or
POS module.

## Detailed Implementation Sequence

### Phase 1 — Align Requirements And Contracts

1. Replace the obsolete profile-creation exclusions in `spec.md` (`FR-003`,
   `FR-036`, User Story 2 acceptance scenario, edge cases, and success
   criteria) with the agreed client-side confirmation behavior.
2. Make the trust boundary explicit:
   - this prevents accidental phone entry errors by a trusted administrator;
   - only an authenticated identity with the SWA custom role `admin` may call
     the SMS endpoint;
   - SWA rejects anonymous and authenticated non-admin requests before the
     Function is invoked;
   - the backend does not attest that the phone was verified;
   - the database stores no verification flag or timestamp.
3. Add the new endpoint to `api-contract.md` using the contract in
   `contracts/admin-profile-activation-sms.openapi.yaml`.
4. Record in lifecycle/storage documentation that the code is ephemeral
   frontend memory and creates no Azure Table entity.
5. Add one implementation-ledger item to the backend and frontend status files.

### Phase 2 — Add The Stateless Backend SMS Endpoint

1. Add route constant:
   `backoffice/customer-profiles/activation-code-sms`. Keeping the endpoint
   below `/api/backoffice/*` is mandatory because that is where the existing
   SWA `allowedRoles: ["admin"]` rule is enforced.
2. Add a request contract with nullable `phone` and nullable `code`. Successful
   provider acceptance returns an empty `202 Accepted`; no response model is
   needed.
3. Extend `AdminCustomerProfileService` instead of creating a second service:
   - normalize with `NormalizedPhoneNumber.TryCreate`;
   - require exactly two ASCII decimal characters;
   - build a fixed Ukrainian message around the supplied code;
   - call the existing `ISmsSender`;
   - return `400 invalid_phone` for the phone, `400 invalid_request` for the
     code/body, and map provider rejection to `502 sms_send_failed`.
4. Add the Function method to the existing
   `AdminCustomerProfilesFunction`; reuse its JSON/error response pattern.
5. Harden the shared `SmsFlyClient` transport boundary so provider network
   failures and provider timeouts become `SmsSendResult(false)` and therefore
   `502`, while caller cancellation still propagates. This fixes the same
   accidental `500` path for the existing public SMS caller.
6. Do not log the request body/code, return the code, write audit events, or
   persist any challenge.
7. Keep Azure unchanged:
   - the existing `/api/backoffice/*` SWA rule already requires the custom role
     `admin`; retain this rule and do not add a less-protected exception;
   - keep the Function trigger consistent with the existing backoffice
     Functions (`AuthorizationLevel.Anonymous`): SWA is the authorization
     boundary, so this trigger setting must not be interpreted as public access;
   - SMS-Fly settings and sender already exist;
   - existing dependency telemetry/alerting observes provider failures;
   - no Bicep, table, cleanup, secret, or POS change is required.
8. Before deployment, verify that the target SWA has `SmsFlyApiKey`,
   `SmsFlySender`, and the already-required `SmsCodeTtlSeconds`. The current
   Bicep managed-settings object does not emit the TTL setting; repair that
   pre-existing configuration drift only if this deployment manages SWA app
   settings.

### Phase 3 — Add Create-Mode Frontend Confirmation

1. Add API types and `sendActivationCodeSms({ phone, code })` to the existing
   `AdminCustomerProfilesApi`.
2. Add create-mode component state:
   - normalized phone for which the current code was generated;
   - current expected two-character code;
   - separate two-character input control;
   - send-in-progress flag;
   - computed code-sent, matched, mismatch, send-enabled, and submit-enabled
     states.
3. On the first send for the current phone, generate the candidate with the
   browser's built-in `Math.random`, format with `padStart(2, '0')`, retain it
   in component memory, and send it with the normalized phone.
4. Reuse that same code for every retry/resend while the phone is unchanged.
   Mark it as sent only after the backend returns `202`. On failure, show the
   shared error toast and keep the same candidate for retry.
5. Subscribe to phone value changes and clear all confirmation state on every
   edit. Clear it again on successful form reset.
6. Render the send action below the phone input. After a successful send, reuse
   the existing Ionic `ion-input-otp` interaction pattern at length two and
   render a non-color-only confirmation message after a match.
7. In create mode, require an exact local match before the existing create
   request can run. Do not add the code or confirmation state to
   `CustomerProfileUpsertRequest`.
8. In edit mode, do not render or require the confirmation controls and preserve
   the current update behavior.

### Phase 4 — Verification

1. Frontend API test: exact endpoint and `{ phone, code }` payload.
2. Form tests:
   - valid phone sends a zero-padded two-character code without questionnaire
     data;
   - create is blocked before a matching code;
   - wrong code remains blocked with visible mismatch feedback;
   - exact match sends the existing unmodified profile payload;
   - phone edit clears confirmation permanently;
   - retry/resend for the same phone reuses the expected code;
   - failed first send does not expose the code-entry UI but keeps the candidate
     for retry;
   - edit mode remains unchanged.
3. Backend verification:
   - build succeeds;
   - malformed JSON, invalid phone, and any code other than two ASCII digits
     return `400`;
   - provider rejection returns `502`;
   - accepted send returns empty `202 Accepted`;
   - response and logs do not contain the code.
4. Manual dev smoke with an approved test phone:
   - no full questionnaire request is sent before confirmation;
   - SMS content contains the same two digits held by the form;
   - wrong digits cannot create;
   - dictated digits unlock the unchanged create call;
   - phone edit, reset, close, and reload require a new send;
   - light/dark mobile layouts remain readable.
5. Deployed auth/config smoke:
   - an authenticated identity with `admin` reaches the endpoint and can
     receive `202`;
   - an authenticated identity without `admin` receives the existing SWA `403`
     handling and the Function is not invoked;
   - an anonymous request is blocked by the existing SWA `401`/login redirect
     handling and the Function is not invoked;
   - verify the deployed `staticwebapp.config.json` still contains
     `/api/backoffice/*` with `allowedRoles: ["admin"]`;
   - the target environment contains all existing SMS-Fly settings.
6. Completion commands:
   - `dotnet build functions/SimpleDiscountVerifier.Api.csproj`
   - `npm --prefix frontend test`
   - `npm --prefix frontend run lint`
   - `npm --prefix frontend run format:check`
   - `npm --prefix frontend run build`

## Error And Edge-Case Matrix

| Situation | Expected behavior |
| --- | --- |
| Anonymous caller sends SMS request | SWA blocks or redirects according to the existing `401` policy; Function is not invoked |
| Authenticated caller lacks `admin` | SWA applies the existing `403` policy; Function is not invoked |
| Authenticated caller has `admin` | Request may reach the Function |
| Phone is syntactically invalid | Send action disabled; no API call |
| Code candidate is `0` | Send `"00"` |
| SMS request is in flight | Send and create actions disabled |
| Provider rejects SMS | Error toast; the same candidate is retained for retry |
| Retry/resend for the same phone | The same code is sent again |
| Delayed SMS arrives after resend | It contains the same expected code |
| Entered value has fewer/more than two digits | Field validation error |
| Entered two digits do not match | Explicit mismatch; create remains disabled |
| Phone changes after a match | Confirmation is cleared |
| Phone changes away and then back | Old confirmation remains cleared |
| Form closes or browser reloads | Memory state disappears; resend required |
| Existing profile is edited | No activation-code UI or requirement |
| Direct create API call | Existing backend behavior remains unchanged by the agreed trusted-admin model |

## Post-Design Constitution Re-Check

All gates remain passed. The design introduces no new dependency, persistent
state, reusable component, custom CSS, page, route, profile field, Azure
resource, or POS behavior. The only deliberate ceiling is that the confirmation
is a frontend operational guard and therefore is not enforced against a direct
API caller; that matches the explicitly agreed trusted-administrator model.

## Complexity Tracking

No constitution violations or complexity exceptions are required.
