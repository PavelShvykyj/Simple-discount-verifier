# Tasks: Client-Side Phone Confirmation During Profile Creation

**Input**: Design documents from `specs/001-discount-verification/`

## Phase 1: Setup

**Purpose**: Confirm the existing extension points and protected route.

- [x] T001 Verify the existing SMS sender, admin profile service/function, Angular profile form/API, and `/api/backoffice/*` admin rule in `functions/`, `frontend/src/`, and `frontend/public/staticwebapp.config.json`

---

## Phase 2: Foundational

**Purpose**: Align the approved requirements and contract before code changes.

- [x] T002 Update the administrator phone-confirmation requirements in `specs/001-discount-verification/spec.md`
- [x] T003 [P] Document the stateless admin-only endpoint and ephemeral frontend state in `docs/architecture/api-contract.md`, `docs/architecture/data-lifecycle.md`, and `docs/architecture/table-storage-design.md`

---

## Phase 3: User Story 2 - Confirm Phone During Profile Creation (Priority: P2)

**Goal**: Let an administrator send a frontend-generated two-digit code to the entered phone and create the profile only after the customer dictates the matching code.

**Independent Test**: In create mode, an admin sends a code, cannot create with a wrong code, can create with the matching code, and loses confirmation after changing the phone; edit mode remains unchanged.

### Tests

- [x] T004 [P] [US2] Add API payload and endpoint tests in `frontend/src/entities/customer-profile/api/admin-customer-profiles.api.spec.ts`
- [x] T005 [P] [US2] Add create/edit phone-confirmation flow tests in `frontend/src/features/customer-profile-form/ui/customer-profile-form.component.spec.ts`

### Backend implementation

- [x] T006 [US2] Add activation SMS request and error contracts in `functions/Contracts/Admin/CustomerProfileRequest.cs` and `functions/Application/CustomerProfiles/CustomerProfileErrorCodes.cs`
- [x] T007 [US2] Implement activation SMS validation and sending in `functions/Application/CustomerProfiles/AdminCustomerProfileService.cs`
- [x] T008 [US2] Add the protected backoffice route and empty-202 Function handler in `functions/Functions/Admin/AdminApiRoutes.cs` and `functions/Functions/Admin/AdminCustomerProfilesFunction.cs`
- [x] T009 [US2] Map SMS transport failures to provider rejection without swallowing caller cancellation in `functions/Infrastructure/Sms/SmsFlyClient.cs`

### Frontend implementation

- [x] T010 [US2] Add the `{ phone, code }` contract and API method in `frontend/src/entities/customer-profile/model/customer-profile.types.ts` and `frontend/src/entities/customer-profile/api/admin-customer-profiles.api.ts`
- [x] T011 [US2] Implement create-only in-memory code generation, resend reuse, matching, reset, and submit gating in `frontend/src/features/customer-profile-form/ui/customer-profile-form.component.ts`
- [x] T012 [US2] Add Ionic send/code/status controls without custom CSS in `frontend/src/features/customer-profile-form/ui/customer-profile-form.component.html`

---

## Phase 4: Polish & Cross-Cutting Concerns

- [x] T013 [P] Update implementation ledgers and product summary in `docs/architecture/backend-api-implementation-status.md`, `docs/architecture/frontend-implementation-status.md`, and `docs/product/discount-verification-summary.md`
- [x] T014 Run backend build plus frontend tests, lint, format check, and production build from `specs/001-discount-verification/quickstart.md`
- [x] T015 Verify the source route remains covered by `/api/backoffice/*` with `allowedRoles: ["admin"]` in `frontend/public/staticwebapp.config.json`

## Dependencies & Execution Order

- T001 → T002/T003 → T004/T005 → T006–T012 → T013–T015.
- T004 and T005 may be prepared independently.
- T006–T009 are sequential backend changes; T010–T012 are sequential frontend changes.
- Deployed SWA role smoke testing remains an environment check after deployment.

## Implementation Strategy

Implement only User Story 2's confirmed creation-time guard. Reuse the existing
SMS sender, admin service/function, entity API, form, Ionic OTP control, and SWA
admin route. Add no storage, new dependency, reusable component, Azure resource,
POS change, or persisted verification field.
