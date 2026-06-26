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

The browser back behavior must be documented during implementation. The default
target behavior is:

- in-flow back controls are explicit UI actions such as "change phone" or
  "start again";
- browser back is not used as the primary flow stepper;
- if implementation adds browser-back-to-previous-step behavior, it must be
  tested on mobile browsers and documented in this file.

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

Component styles should avoid hard-coded measurements and colors. Prefer Ionic
components, Ionic utility classes, Ionic CSS variables, and the small set of
existing application CSS variables before adding new custom variables.

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

## State Management Decision Point

The accepted implementation shape is a provider-scoped flow store owned by the
route page or route shell. The store may be implemented with Angular signals
first. Adding `@ngrx/signals` is a separate dependency decision and should be
made before the public flow PR if the team wants the external Signal Store API
instead of a small project-local signal store.

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
- document final browser back behavior.

Verification:

- production build;
- Lighthouse mobile report captured in PR notes;
- manual checks on narrow mobile viewport and at least one desktop viewport;
- accessibility checks for labels, focus, and color contrast.
