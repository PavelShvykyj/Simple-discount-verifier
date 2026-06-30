<!--
Sync Impact Report
Version change: 1.2.0 -> 1.3.0
Modified principles: II. Angular and Ionic Only; III. Feature-Sliced Design Boundaries; V. Quality and Accessibility Gates; VII. Page Design Review And Dumb Reusable Components
Added principles: none
Added sections: none
Removed sections: none
Templates requiring updates:
- .specify/templates/plan-template.md: updated for Angular control flow, signal API, and template binding gates
- .specify/templates/spec-template.md: updated for Angular control flow, signal API, and template binding requirements
- .specify/templates/tasks-template.md: updated for Angular control flow, signal API, and template binding tasks
- .specify/templates/checklist-template.md: reviewed, no update required
Follow-up TODOs: none
-->

# Restaurant Discount Verifier Frontend Constitution

## Core Principles

### I. Mobile-First Frontend

The frontend MUST be designed and implemented for mobile screens first because
approximately 98% of real usage is expected on mobile devices. The public
customer discount redemption flow MUST be optimized for one-handed mobile use,
short forms, tolerant mobile input, fast phone number and SMS code entry,
readable barcode display, clear small-screen loading/error/retry/expired-code
states, reliable touch targets, and no horizontal scrolling. Desktop layouts MAY
enhance the experience, but MUST NOT define or compromise the core mobile UX.

### II. Angular and Ionic Only

Frontend implementation MUST use Angular and Ionic consistently. The frontend
MUST NOT introduce additional frontend frameworks, UI kits unless this constitution and the architecture documentation are
explicitly amended. Backend and hosting context MUST remain aligned with the
accepted architecture decisions: Azure Static Web Apps for hosting context and
managed Azure Functions under `/api` for backend context.

Angular templates MUST use modern Angular control flow blocks (`@if`, `@for`,
`@switch`) instead of legacy structural directives such as `*ngIf` and `*ngFor`.
The only allowed exception is a library directive that has no Angular control
flow equivalent, such as Angular CDK virtual scroll's `*cdkVirtualFor`.
Components MUST use signal-based Angular APIs such as `input()`, `output()`,
`model()`, `viewChild()`, `viewChildren()`, `contentChild()`, and
`contentChildren()` instead of decorator APIs such as `@Input`, `@Output`,
`@ViewChild`, `@ViewChildren`, `@ContentChild`, and `@ContentChildren`.

Templates MUST NOT call component methods or arbitrary functions to derive
display state, validation state, classes, labels, disabled flags, or other bound
values. Derived template state MUST be exposed as signal-based properties using
`computed`, `linkedSignal`, Angular forms state, or equivalent Angular reactive
primitives. Templates MAY read signals/computed signals using Angular signal
syntax and MAY call event-handler commands for user actions.

### III. Feature-Sliced Design Boundaries

Frontend code MUST follow Feature-Sliced Design. The required layers are:
`app` for bootstrap, routing, global providers, and app-wide configuration;
`pages` for route-level screens; `widgets` for larger composed UI blocks used by
pages; `features` for user-facing business actions and flows; `entities` for
business domain models and entity-specific UI/API logic; and `shared` for
reusable infrastructure, UI primitives, utilities, API client, constants, and
low-level helpers. Shared code MUST be genuinely reusable and MUST NOT depend on
higher FSD layers. Cross-feature coupling MUST go through entities or shared
contracts. Reusable UI components MUST be independent presentational components
unless explicitly justified otherwise: they MUST NOT own business flow, API
calls, routing, global store access, feature-specific state, or dependencies on
higher FSD layers.

### IV. Flow Separation and Logic Placement

The public customer redemption flow and administrator customer profile
management flow MUST remain separated in frontend structure and business logic.
Business logic MUST NOT be placed directly in Ionic components when it belongs
in feature, entity, application, or shared services. Customer redemption,
administrator profile management, and shared API/UI foundations MUST remain
independently understandable and testable.

### V. Quality and Accessibility Gates

All frontend code MUST pass ESLint and Prettier checks before it is considered
complete. The frontend MUST target WCAG AA accessibility: interactive controls
MUST be keyboard accessible, forms MUST have clear labels, validation messages,
and focus behavior, color contrast MUST meet WCAG AA, touch targets MUST be
suitable for mobile use, and important state MUST NOT rely on color alone.
Every new or changed page MUST be checked in both light and dark theme modes
before completion, including contrast, focus visibility, validation messages,
loading/error states, and readable interactive controls. SMS verification and
barcode display flows MUST remain usable on mobile screens.

### VI. Ionic-First UI Composition

Frontend pages and UI flows MUST prioritize Ionic components, Ionic CSS utility
classes, and Ionic CSS variables. Custom CSS classes, custom layout primitives,
or new app-specific CSS variables MUST NOT be introduced by default. If a task
appears to require custom CSS or a custom UI primitive, the implementer MUST
ask for confirmation first and describe why Ionic components/utilities are not
sufficient, what trade-off the custom styling introduces, and how accessibility
and light/dark theme behavior will be verified. Existing custom CSS may be
maintained when necessary, but new work MUST prefer Ionic composition and the
existing application variables before expanding the styling surface.

### VII. Page Design Review And Dumb Reusable Components

Before implementing any new page or materially changing an existing page, the
work MUST include a page design review. The review MUST analyze compliance with
mobile UX/UI best practices for the page's primary mobile scenario, including
task priority, one-handed use, touch target size, input ergonomics, keyboard
behavior, navigation clarity, loading/error/empty states, readability, and
visual hierarchy. The review MUST also analyze whether any parts of the page
should become reusable components according to the Feature-Sliced Design
boundaries.

Reusable UI components MUST be "dumb" and absolutely independent by default:
they receive data and configuration through signal-based Angular inputs, expose
user actions through signal-based outputs or content projection, use
Ionic-first composition, and contain no domain decisions, API calls, router
navigation, auth checks, stores, or feature-specific side effects. Business
flow stays in pages, features, entities, or services that compose those
components. If a reusable component needs more responsibility than this, the
exception MUST be documented and approved before implementation.

## Frontend Scope and Product Areas

This constitution currently applies only to the frontend application. Expected
frontend product areas at this stage are:

- public customer discount redemption flow;
- administrator customer profile management flow;
- shared API and UI foundation.

Open questions MUST remain explicit in product or architecture documentation and
MUST NOT be silently converted into implementation assumptions.

## Governance

This constitution governs frontend architecture, mobile-first UX, page design
review, reusable component boundaries, Ionic-first composition, accessibility,
theme verification, and quality gates for this project. Frontend specs, plans,
tasks, and reviews MUST verify compliance with the principles above. If
frontend architecture, folder structure, mobile-first behavior, page design
review rules, reusable component boundaries, Ionic composition rules,
accessibility rules, theme verification, or quality rules change, the relevant
files in `docs/architecture` or `docs/product` MUST be updated in the same
task.

Amendments require an explicit constitution update, a Sync Impact Report, and
review of affected Spec Kit templates. Versioning follows semantic versioning:
MAJOR for incompatible governance or principle redefinitions, MINOR for new or
materially expanded principles or sections, and PATCH for clarifications that do
not change meaning.

**Version**: 1.3.0 | **Ratified**: 2026-05-27 | **Last Amended**: 2026-06-30
