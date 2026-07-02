# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., TypeScript with Angular version, Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]

**Primary Dependencies**: [e.g., Angular + Ionic for frontend, FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]

**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]

**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]

**Target Platform**: [e.g., mobile-first web on modern mobile browsers, Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]

**Project Type**: [e.g., frontend web app, web-service, library/cli/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]

**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]

**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]

**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

[Gates determined based on constitution file]

For frontend work, the plan MUST answer these gates before Phase 0 research and
again after Phase 1 design:

- Mobile-first UX: primary flows are designed for mobile screens first,
  one-handed customer redemption, no horizontal scrolling, readable barcode, and
  small-screen loading/error/retry/expired-code states.
- Page design review: every new or materially changed page has a documented
  mobile UX/UI best-practice review covering primary task clarity, one-handed
  use, touch targets, input ergonomics, mobile keyboard behavior, navigation,
  loading/error/empty/success states, readability, and no horizontal scrolling.
- Technology stack: Angular + Ionic are used consistently with no additional
  frontend frameworks, state managers, or UI kits introduced by default.
- Angular modern syntax: templates use `@if`, `@for`, and `@switch` instead of
  `*ngIf`/`*ngFor` except for library directives with no block equivalent, such
  as Angular CDK virtual scroll's `*cdkVirtualFor`.
- Angular signal APIs: component contracts and queries use `input()`,
  `output()`, `model()`, `viewChild()`, `viewChildren()`, `contentChild()`, and
  `contentChildren()` instead of decorator APIs such as `@Input`, `@Output`,
  and `@ViewChild`.
- Template bindings: templates do not call component methods or arbitrary
  functions to derive bound values; derived state is exposed through `computed`,
  `linkedSignal`, Angular forms state, or equivalent reactive properties.
- Ionic-first UI: pages are composed from Ionic components, Ionic CSS utility
  classes, and Ionic CSS variables first; any new custom CSS class, custom
  layout primitive, or app-specific CSS variable is pre-approved with necessity
  and trade-off documented.
- Feature-Sliced Design: source layout uses `app`, `pages`, `widgets`,
  `features`, `entities`, and `shared`, with dependencies flowing only from
  higher layers toward lower layers.
- Reusable component analysis: page design identifies reusable candidates and
  places them in the correct FSD layer. Reusable UI components are dumb and
  independent by default: signal-based inputs, signal-based outputs, or content
  projection only, with no API calls, routing, auth checks, store access,
  business workflow, or higher-layer dependencies.
- Flow separation: public redemption and administrator profile management remain
  separated unless integration is explicitly justified through entities or
  shared contracts.
- Quality gates: ESLint and Prettier checks are part of completion criteria.
- Accessibility: WCAG AA, keyboard accessibility, mobile-sized touch targets,
  labels, validation messages, focus behavior, and non-color-only states are
  planned and checked in both light and dark theme modes.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── app/
│   ├── pages/
│   ├── widgets/
│   ├── features/
│   ├── entities/
│   └── shared/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
