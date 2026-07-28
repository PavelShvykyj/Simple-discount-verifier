# Frontend Application Structure

This document fixes the planned Angular/Ionic application shell, routing,
support QR behavior, and frontend implementation PR sequence.

## Goals

- Keep the public customer flow extremely small on first load.
- Keep the public flow and staff tools separated by route, navigation, and
  authorization boundary.
- Preserve mobile-first Ionic interaction patterns for both customers and staff.
- Keep scanner, QR, and barcode libraries out of the public first-load bundle
  unless a user action or flow state needs them.

## Page Design Review

Every new page, and every material change to an existing page, must start with a
short page design review before implementation. The review must check the page
against mobile UX/UI best practices for the primary mobile task:

- task priority and whether the most common action is obvious;
- one-handed reach, touch target size, and spacing;
- mobile keyboard behavior, input mode, autocomplete, labels, and validation;
- navigation clarity, including back/restart behavior and route boundaries;
- loading, error, retry, empty, disabled, and success states;
- readable visual hierarchy on narrow screens without horizontal scrolling;
- light and dark theme behavior and WCAG AA accessibility concerns.

The same review must identify whether parts of the page should become reusable
components under Feature-Sliced Design. Reusable UI primitives belong in
`shared/ui`; larger cross-page composed blocks belong in `widgets`; business
actions and flow-specific screens remain in `features` or `pages`.

Reusable components must be dumb and independent by default. They may receive
data through signal-based Angular inputs, expose user actions through
signal-based outputs, and project content, but they must not call APIs,
navigate with the router, read auth state, own business workflow decisions,
access stores, or depend on higher FSD layers. Pages, features, entities, or
services own that behavior and compose the dumb components.

## Route Shells

The application has two route shells:

```text
/
  PublicRedemptionPage

/admin
  AdminShell
    /admin/customers/*
    /admin/service/*
```

The public route is anonymous. The `/admin*` route is protected both by Azure
Static Web Apps route rules and by Angular `adminGuard`.

There is no visible navigation path from the public flow to the staff area. Staff
can open the public flow by directly visiting `/`. A staff-side shortcut to `/`
may be added later if there is an operational need for training or assisted
customer flow testing, but the public UI must not advertise the staff area.

## Public Redemption Flow

The public customer flow remains one Angular route at `/`. It is not split into
separate Angular routes for phone, SMS, and barcode screens.

`PublicRedemptionPage` owns:

- an Ionic `ion-nav` internal stack;
- a provider-scoped redemption flow store;
- the quiet support action;
- a quiet theme-mode action for system, light, and dark modes;
- the flow-level lifetime for `correlationId`, `redemptionKey`, barcode data,
  error state, retry timers, and expiry state.

The public route shell must follow the standard Ionic page structure:
`ion-header`, one non-fullscreen `ion-content`, and the optional shell footer.
Do not set `fullscreen` on the public shell content because the internal
`ion-nav` screens must start below the header, not behind it.

The initial `ion-nav` root is the phone-entry screen. Later screens are pushed
onto the stack only after the business flow allows them:

```text
PhoneEntryScreen
  submit phone -> POST /api/public/redemptions
  success with smsSent=true -> push SmsVerificationScreen

SmsVerificationScreen
  verify code -> POST /api/public/redemptions/{redemptionKey}/sms-verifications
  success -> push BarcodeResultScreen
  change phone / restart -> reset store and set root PhoneEntryScreen

BarcodeResultScreen
  expiry / new code request -> reset store and set root PhoneEntryScreen
```

Errors remain state inside the current screen or in small Ionic overlays. Error
states are not separate routes and are not separate `ion-nav` screens unless a
future UX decision explicitly needs a dedicated recovery screen.

Browser back must not control the internal public redemption steps. Target
behavior:

- in-flow back controls are explicit UI actions such as "change phone" or
  "start again";
- browser back leaves the current route according to normal browser history and
  does not pop the phone/SMS/barcode `ion-nav` stack.

## Public Performance Rules

The public first-load route includes only what is needed to show and submit the
phone-entry screen.

The following must be loaded dynamically:

- SMS verification screen;
- barcode result screen;
- support QR dialog;
- QR generation helper;
- barcode rendering helper;
- scanner/camera libraries, which should not be part of the public flow unless a
  future public camera feature is explicitly added.

The implementation should keep external fonts and decorative media out of the
public first screen. The UI should use system fonts and small Ionic components
for fast mobile rendering.

## Public Support Action

The public page shows a quiet support icon action independent of the current
phone/SMS/barcode screen. It must not be styled as the primary call to action.

Behavior:

- Before `correlationId` exists, tapping the action shows a small explanation
  that the staff support code appears after verification starts. It must not
  call the backend and must not lazy-load the QR generator.
- After `correlationId` exists, tapping the action lazy-loads and opens the
  support QR dialog.
- Business errors may additionally render an inline secondary action that opens
  the same support dialog when `correlationId` exists.

The support QR payload is a neutral text payload, not a URL:

```text
SDV-SUPPORT:v1:<correlationId10>
```

Example:

```text
SDV-SUPPORT:v1:QPS7O7KCNM
```

The dialog should also show the plain `correlationId` text so staff can enter it
manually if scanning fails.

If a customer scans the QR with their own camera, the result is only a text code.
It must not navigate the customer to the admin area or trigger Static Web Apps
login.

## Theme Mode Control

The application keeps the existing system, light, and dark theme modes.
`ThemeModeService` remains shared application infrastructure rather than a
feature-specific service.

The theme selector must be a separate reusable UI component backed by
`ThemeModeService`. The current visual design of the theme toggle is not the
target design and should be replaced during shell implementation.

Placement:

- Public route `/`: show the theme-mode control as a quiet secondary icon or
  compact action in the public page shell. It must not compete with the phone,
  SMS, barcode, or support actions.
- Staff route `/admin`: show the theme-mode control in `AdminShell`, so it is
  available across `/admin/customers/*` and `/admin/service/*` without being
  duplicated on every staff page.

The selected mode should persist according to the existing theme service
behavior. Theme switching must not reset the public redemption `ion-nav` stack
or the provider-scoped redemption flow store.

Recommended mobile UI:

- Entry point: one icon button in the page/shell chrome. Use the active mode
  icon (`sunny`, `moon`, or `phone-portrait` / system equivalent) and an
  accessible label.
- Selection surface: open an Ionic `ion-popover` on wider screens and a compact
  `ion-action-sheet` or small `ion-modal` sheet on narrow mobile screens.
- Options: three rows with icon, label, and selected checkmark:
  `System`, `Light`, and `Dark`.
- Avoid a three-segment control permanently visible in the public flow; it takes
  too much visual weight for a secondary preference.
- Avoid a binary light/dark switch; it hides the required system mode.

## Angular Component Files And Styling

Angular components must keep markup and styles in separate files. Do not use
inline `template` or `styles` in the component decorator for application
components.

Angular templates must use modern control flow blocks: `@if`, `@for`, and
`@switch`. Do not use legacy `*ngIf` or `*ngFor`. The only allowed exception is
a library directive that has no Angular block equivalent, such as Angular CDK
virtual scroll's `*cdkVirtualFor`.

Components must use signal-based Angular APIs for component contracts and view
queries: `input()`, `output()`, `model()`, `viewChild()`, `viewChildren()`,
`contentChild()`, and `contentChildren()`. Do not use decorator APIs such as
`@Input`, `@Output`, `@ViewChild`, `@ViewChildren`, `@ContentChild`, or
`@ContentChildren` in new frontend code.

Templates must not call component methods or arbitrary functions to compute
bound values. Put derived display state, validation state, CSS class flags,
labels, disabled flags, and similar values into signal-based properties such as
`computed`, `linkedSignal`, Angular forms state, or equivalent Angular
reactive primitives. Templates may read signals/computed signals and may call
event-handler commands for user actions.

Page and component UI must be Ionic-first. Use Ionic components, Ionic utility
classes, Ionic CSS variables, and the small set of existing application CSS
variables before adding custom CSS.

Visible button labels must be written and displayed in normal case, not forced
to uppercase. Icon-only buttons remain icon-only and must provide accessible
labels.

Do not create new custom CSS classes, custom layout primitives, or new
app-specific CSS variables by default. If a page appears to require custom CSS,
pause and ask for confirmation. The confirmation request must explain:

- why Ionic components, Ionic utility classes, or existing variables are not
  sufficient;
- which custom class, primitive, or variable would be added;
- the trade-off introduced by the custom styling;
- how WCAG AA behavior and light/dark theme behavior will be verified.

Every new or changed page must be checked in both light and dark themes before
completion. The check must include contrast, focus visibility, labels,
validation messages, touch targets, loading/error states, and non-color-only
state communication.

## Admin Shell

`/admin` uses Ionic tabs as the top-level staff navigation:

```text
AdminShell
  ion-tabs
    Customers tab -> /admin/customers
    Service tab -> /admin/service
```

Tabs are preferred for the current staff area because there are only two
top-level groups and the application is mobile-first. A side menu or
`ion-split-pane` may be introduced later if the staff area grows beyond the
current route groups.

## ADMIN_CUSTOMERS_ROUTES

The customer profile route group is planned as:

```text
/admin/customers
/admin/customers/new
/admin/customers/by-phone/:phone
/admin/customers/by-phone/:phone/edit
```

Route responsibilities:

- `/admin/customers`: searchable list and point lookup by phone.
- `/admin/customers/new`: create a saved customer profile.
- `/admin/customers/by-phone/:phone`: view one customer profile.
- `/admin/customers/by-phone/:phone/edit`: edit profile fields, including the
  phone-change case supported by the backend contract.

The backend uses normalized phone as the profile identity in the MVP; therefore
the frontend route uses `by-phone/:phone` rather than a synthetic `profileId`.
The path phone value must be URL-encoded when it contains `+`.

## ADMIN_SERVICE_ROUTES

The service route group is planned as:

```text
/admin/service
/admin/service/inspect
/admin/service/health
/admin/service/scanner-survey
```

Route responsibilities:

- `/admin/service`: compact service hub for staff tools.
- `/admin/service/inspect`: inspect a redemption attempt by `correlationId`,
  support QR payload, or `barcodeValue`.
- `/admin/service/health`: call and display `/api/system/health`.
- `/admin/service/scanner-survey`: migrated scanner compatibility survey.

The inspect route may accept query parameters for staff-created links, but the
public support QR does not contain an admin URL. When the admin scanner reads
`SDV-SUPPORT:v1:<correlationId10>`, the admin frontend parses it and submits:

```json
{
  "correlationId": "QPS7O7KCNM"
}
```

to `POST /api/backoffice/redemptions/inspect`.

## Public Flow State Management

The public redemption flow uses a provider-scoped store owned by
`PublicRedemptionPage`. The PR-2 implementation uses Angular signals without
adding `@ngrx/signals`.

Components must depend on a public store interface exposed through an Angular
`InjectionToken`, not directly on the concrete service class. This keeps the
phone, SMS, and barcode screens independent from the implementation and leaves a
clear migration path to `@ngrx/signals` later if multiple frontend stores need a
shared store framework.

## PR Plan

### PR-1. Frontend Shell And Routing Baseline

Scope:

- create `/admin` shell with Ionic tabs;
- preserve the existing system/light/dark theme-mode control in the public page
  shell and the new admin shell as a separate reusable component with updated
  mobile-first Ionic UI;
- add lazy route groups for `/admin/customers` and `/admin/service`;
- move or wrap the existing scanner survey under
  `/admin/service/scanner-survey`;
- keep legacy `/scanner-survey*` protected during migration, either as a
  redirect or as a temporary compatibility route;
- update `staticwebapp.config.json` if route protection changes.
- add local frontend API proxy support for `npm run start:local-api`, targeting
  the local Functions host while allowing that local host to use the current
  test Azure resources through uncommitted `functions/local.settings.json`.

Verification:

- `npm --prefix frontend run lint`;
- `npm --prefix frontend run build`;
- built `staticwebapp.config.json` still contains protected `/admin*` and
  scanner-survey rules.

### PR-2. Public Redemption Flow Skeleton

Scope:

- replace the temporary home page with `PublicRedemptionPage`;
- add provider-scoped redemption flow store;
- implement `ion-nav` phone, SMS, and barcode screen stack;
- add typed API client contracts for the two public endpoints;
- implement loading, retry, and error state surfaces without barcode rendering.

Phone confirmation page design review:

- Primary mobile task: enter a Ukrainian phone number and request an SMS code
  while standing at payment time.
- Mobile UX/UI: use one short form with a local Angular `FormControl`,
  `type="text"`, `inputmode="numeric"`, `enterkeyhint="next"`,
  `autocomplete="tel-national"`, and a visible example `501234567`. The user
  enters only the nine digits after `+380`, while `+380` remains visible in the
  Ionic input start slot; the flow normalizes to E.164 before calling the API.
  Keep a full-width primary Ionic button inside the form card under the input.
  Keep the card aligned near the top under the header using the same standard
  grid padding as the horizontal screen edges, and keep an empty Ionic toolbar
  footer available for safe-area balance without duplicating the primary action
  there.
- Validation: block incomplete or incorrectly formatted Ukrainian phone numbers
  before calling `/api/public/redemptions`; show the Ionic input error only when
  the phone control is touched and invalid, and preserve backend/business errors
  as alert text.
- Loading and disabled states: the primary button remains present, communicates
  disabled state through the native Ionic disabled state, and shows readable
  loading text with the spinner during submit.
- Reusable component analysis: the route page composes the provider-scoped flow
  store and screen stack; reusable layout remains in `shared/ui`, while phone
  validation and flow transitions stay in `features/redemption-flow`.
- Theme/accessibility: verify the screen in dark and light modes for contrast,
  readable helper/error text, focus visibility, touch targets, and non-color-only
  error communication.

SMS confirmation page design review:

- Primary mobile task: enter the six-digit SMS code and move to the barcode
  screen without restarting the customer flow.
- Mobile UX/UI: use Ionic `ion-input-otp` with numeric input mode, six boxes,
  one full-width primary confirmation button, a secondary resend action, and a
  clear "change number" action. The screen must keep the phone number visible in
  normalized display form so the customer can confirm where the SMS was sent.
- Validation: keep a local Angular control for the SMS code. Block submit until
  six digits are entered, show the local validation error only after the SMS
  control is touched and invalid, and keep backend verification errors as alert
  text separate from local validation.
- Resend: use the existing public redemption start endpoint for the current
  phone through the provider-scoped flow store. The resend action must respect
  `retryAfterSeconds`, show a countdown while disabled, clear the entered SMS
  code after a successful resend, and keep the current `ion-nav` SMS screen open
  even if resend returns a retryable business error.
- Loading and disabled states: SMS verification and SMS resend must expose
  separate busy states using Ionic disabled buttons, spinner text, and
  `aria-busy`.
- Theme/accessibility: verify the SMS code boxes, validation note, resend
  countdown, backend error note, and all actions in light and dark themes with
  WCAG AA contrast and non-color-only state communication.

Verification:

- unit tests for store transitions and API error mapping;
- mobile viewport smoke test for phone and SMS screens;
- lint and build.

### PR-3. Public Barcode And Support QR

Scope:

- dynamically load barcode rendering only for the barcode result screen;
- add always-visible quiet support icon action;
- implement pre-correlation explanation state;
- implement lazy support QR dialog with payload
  `SDV-SUPPORT:v1:<correlationId10>`;
- show plain `correlationId` text in the dialog.

Verification:

- tests for support payload formatting and correlation id validation;
- confirm QR/barcode helpers are not in the initial public route chunk;
- mobile visual check for barcode and support dialog.

### PR-4. Admin Customer Profiles

Scope:

- implement `/admin/customers` list and phone lookup;
- implement create, detail, and edit pages;
- add field definitions for phone, full name, birth date, and favorite dish;
- integrate `/api/backoffice/customer-profiles` endpoints;
- handle duplicate profile, invalid phone, and validation errors.

Verification:

- form validation tests;
- API client tests or mocked component tests;
- mobile viewport checks for create/edit forms;
- lint and build.

The later physical-card-number extension is not part of the completed PR-4
baseline. Its manual entry, mobile-camera scan, validation, and API changes are
tracked separately as `PCN-03` in
`docs/architecture/physical-card-number-implementation-plan.md`.

### PR-5. Admin Service Inspect And Health

Scope:

- implement `/admin/service` hub;
- implement inspect by manual `correlationId` and `barcodeValue`;
- implement admin camera scanner for `SDV-SUPPORT:v1:<correlationId10>` with
  dynamically loaded scanner library;
- implement `/admin/service/health`;
- render redemption status, barcode status, profile summary, scan context, and
  audit event timeline.

Verification:

- parser tests for support QR payload and barcode/correlation inputs;
- mocked inspect response rendering tests;
- scanner library remains isolated to the service chunk;
- lint and build.

### PR-6. Performance And Mobile UX Hardening

Scope:

- run Lighthouse/Chrome mobile audits against the production build;
- tune initial bundle budgets if needed;
- verify no admin/scanner/barcode helpers are in the public first-load path;
- polish mobile focus behavior, keyboard behavior, safe-area spacing, and
  one-handed operation;
- verify that browser back does not drive the public phone/SMS/barcode steps.

Verification:

- production build;
- Lighthouse mobile report captured in PR notes;
- manual checks on narrow mobile viewport and at least one desktop viewport;
- accessibility checks for labels, focus, and color contrast.
