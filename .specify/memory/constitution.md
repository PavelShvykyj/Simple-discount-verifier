<!--
Sync Impact Report
Version change: N/A -> 1.0.0
Modified principles: initial ratification
Added sections: Frontend Scope and Product Areas; Governance
Removed sections: template placeholder sections
Templates requiring updates:
- .specify/templates/plan-template.md: updated
- .specify/templates/spec-template.md: updated
- .specify/templates/tasks-template.md: updated
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
MUST NOT introduce additional frontend frameworks, UI kits, state managers, or
deployment rules unless this constitution and the architecture documentation are
explicitly amended. Backend and hosting context MUST remain aligned with the
accepted architecture decisions: Azure Functions for backend context and Azure
for hosting context.

### III. Feature-Sliced Design Boundaries

Frontend code MUST follow Feature-Sliced Design. The required layers are:
`app` for bootstrap, routing, global providers, and app-wide configuration;
`pages` for route-level screens; `widgets` for larger composed UI blocks used by
pages; `features` for user-facing business actions and flows; `entities` for
business domain models and entity-specific UI/API logic; and `shared` for
reusable infrastructure, UI primitives, utilities, API client, constants, and
low-level helpers. Shared code MUST be genuinely reusable and MUST NOT depend on
higher FSD layers. Cross-feature coupling MUST go through entities or shared
contracts.

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
suitable for mobile use, and important state MUST NOT rely on color alone. SMS
verification and barcode display flows MUST remain usable on mobile screens.

## Frontend Scope and Product Areas

This constitution currently applies only to the frontend application. Expected
frontend product areas at this stage are:

- public customer discount redemption flow;
- administrator customer profile management flow;
- shared API and UI foundation.

Open questions MUST remain explicit in product or architecture documentation and
MUST NOT be silently converted into implementation assumptions.

## Governance

This constitution governs frontend architecture, mobile-first UX, accessibility,
and quality gates for this project. Frontend specs, plans, tasks, and reviews
MUST verify compliance with the principles above. If frontend architecture,
folder structure, mobile-first behavior, accessibility rules, or quality rules
change, the relevant files in `docs/architecture` or `docs/product` MUST be
updated in the same task.

Amendments require an explicit constitution update, a Sync Impact Report, and
review of affected Spec Kit templates. Versioning follows semantic versioning:
MAJOR for incompatible governance or principle redefinitions, MINOR for new or
materially expanded principles or sections, and PATCH for clarifications that do
not change meaning.

**Version**: 1.0.0 | **Ratified**: 2026-05-27 | **Last Amended**: 2026-05-27
