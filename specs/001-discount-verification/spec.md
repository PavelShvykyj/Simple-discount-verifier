# Feature Specification: Restaurant Discount Verification

**Feature Branch**: `001-discount-verification`

**Created**: 2026-05-27

**Status**: Draft

**Input**: User description: "Build a web-based discount verification system for a restaurant business."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Redeem Discount With Verified Phone (Priority: P1)

A pre-approved customer opens the public discount page at the time of payment,
enters their phone number, confirms ownership with an SMS code, receives a
short-lived one-time barcode, and shows it to the cashier for scanning.

**Why this priority**: This is the core value of the system: the restaurant can
grant a discount only when the person requesting it proves access to the phone
number registered in a saved customer profile.

**Independent Test**: Create a saved customer profile with a phone number, run
the public redemption flow on a mobile-sized screen, verify the phone by SMS,
display a barcode, validate the scanned code once, and confirm a second
validation attempt fails.

**Acceptance Scenarios**:

1. **Given** a saved customer profile exists for the entered phone number,
   **When** the customer enters the phone number and a valid SMS code, **Then**
   the system displays a short-lived one-time barcode associated with that
   verified phone number.
2. **Given** no saved customer profile exists for the entered phone number,
   **When** the customer submits the phone number, **Then** the process stops
   without sending an SMS code and without issuing a barcode.
3. **Given** an SMS code is invalid, expired, or has exceeded allowed attempts,
   **When** the customer submits it, **Then** the process fails and no barcode is
   issued.
4. **Given** a one-time barcode has already been validated successfully, **When**
   the same barcode is presented again, **Then** validation fails and no discount
   lookup key is returned.

---

### User Story 2 - Register Eligible Customer Profile (Priority: P2)

An administrator signs in to a protected administrative area and creates a saved
customer profile for a customer who has been approved to receive a discount,
including the phone number that will later connect the profile to the discount
card in the main restaurant application.

**Why this priority**: Redemption can only work for pre-approved customers, so
the restaurant needs a simple way to record eligible customers before payment.

**Independent Test**: Sign in as an administrator, create a customer profile
with a phone number, confirm it is saved without SMS verification during profile
creation, and use that phone number to start the redemption flow.

**Acceptance Scenarios**:

1. **Given** an authorized administrator, **When** they enter required profile
   data including a phone number and save the profile, **Then** the customer
   profile exists and can be found later by that phone number.
2. **Given** an administrator is creating or updating a profile, **When** they
   save the phone number, **Then** the system does not require SMS verification
   during profile creation in the initial release.
3. **Given** an administrator is not authorized, **When** they try to access the
   administrative profile area, **Then** they cannot create or update customer
   profiles.

---

### User Story 3 - Validate Web Barcode From Main Application (Priority: P3)

The main restaurant application scans a barcode, keeps using its existing
discount card flow for standard EAN13 cards, and asks the discount verification
service to validate web-generated one-time codes.

**Why this priority**: The restaurant's existing payment flow must remain in
control of discount card lookup and discount calculation while the new system
only proves verified phone access.

**Independent Test**: Present a web-generated one-time barcode to the main
restaurant application, validate it with the discount verification service,
confirm the service returns the agreed lookup key once, and confirm invalid,
expired, used, and unknown codes fail.

**Acceptance Scenarios**:

1. **Given** a scanned value is a standard EAN13 discount card barcode, **When**
   the main restaurant application processes it, **Then** the existing discount
   card process continues without using the web-code validation flow.
2. **Given** a scanned value is a web-generated one-time code, **When** the main
   restaurant application requests validation, **Then** the service validates
   the code and returns the phone number or other agreed lookup key only if the
   code is valid and unused.
3. **Given** a one-time code is invalid, expired, already used, or unknown,
   **When** validation is requested, **Then** the service returns a failed
   validation result and no discount is applied based on that code.
4. **Given** a one-time code validates successfully, **When** the validation is
   completed, **Then** the code is immediately deleted or invalidated and cannot
   be restored after a later sale cancellation.

---

### User Story 4 - Trace Redemption and Fraud-Relevant Events (Priority: P4)

Restaurant support or authorized staff can trace a discount redemption flow from
phone verification request through barcode issue, validation, failure,
expiration, or invalidation without exposing the raw phone number in operational
logs.

**Why this priority**: Traceability supports troubleshooting and fraud
investigation, especially because the system is intended to reduce staff fraud
risk.

**Independent Test**: Complete successful and failed redemption flows and verify
that each important business event is logged with a correlation id, that the
correlation id is not the raw phone number, and that troubleshooting can use a
phone hash where appropriate.

**Acceptance Scenarios**:

1. **Given** a customer starts the redemption flow, **When** SMS verification is
   requested, **Then** the system creates a random correlation id that follows
   the flow through terminal events.
2. **Given** important business events occur, **When** they are recorded, **Then**
   they include the correlation id and avoid using the raw phone number as the
   correlation id.
3. **Given** support needs to troubleshoot by phone number, **When** logs are
   reviewed, **Then** a phone hash can support lookup without exposing the raw
   phone number in logs.

### Edge Cases

- Customer mistypes a phone number on a mobile keyboard.
- Customer submits a phone number for which no saved profile exists.
- SMS send fails or is delayed.
- Customer submits an invalid, expired, or over-attempt SMS code.
- Customer requests a new barcode while another active barcode exists for the
  same phone number.
- Barcode expires before the cashier scans it.
- Barcode is scanned more than once.
- Barcode validates successfully, but the sale is later cancelled.
- Main restaurant application scans a standard EAN13 discount card instead of a
  web-generated one-time code.
- Main restaurant application cannot find a discount card by the returned lookup
  key.
- Web-generated barcode format or prefix is not reliably distinguishable from
  EAN13 during scanner compatibility testing.
- Public redemption screens are used on small mobile screens with one-handed
  interaction, mobile keyboard input, loading, retry, and expired-code states.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a protected administrative area where an
  authorized administrator can create and update customer profiles.
- **FR-002**: A customer profile MUST include a phone number used as the primary
  identifier for later discount redemption and discount card lookup.
- **FR-003**: The initial release MUST NOT require SMS verification during
  administrator-managed profile creation.
- **FR-004**: The initial release MUST NOT include complex customer profile
  workflow states such as Draft, PendingSync, Synced, or SyncError.
- **FR-005**: The public customer redemption flow MUST allow a customer to enter
  their phone number manually.
- **FR-006**: The public customer redemption flow MUST be mobile-first, avoid
  horizontal scrolling, use clear short forms, support reliable touch
  interaction, and provide clear loading, error, retry, and expired-code states.
- **FR-007**: The system MUST search for a saved customer profile by the
  customer-entered phone number before sending any SMS code.
- **FR-008**: If no saved customer profile exists for the entered phone number,
  the system MUST stop the redemption process without sending an SMS code and
  without issuing a barcode.
- **FR-009**: If a saved customer profile exists, the system MUST send an SMS
  verification code to the same phone number entered by the customer.
- **FR-010**: The system MUST reject invalid, expired, or over-attempt SMS code
  submissions and MUST NOT issue a barcode for those failures.
- **FR-011**: The system MUST issue a one-time discount code only after
  successful SMS verification during the discount redemption process.
- **FR-012**: A one-time discount code MUST be random, hard to guess,
  short-lived, and usable only once.
- **FR-013**: The default one-time discount code lifetime SHOULD be 3 minutes and
  MUST be configurable.
- **FR-014**: The system MUST ensure that no more than one active one-time
  discount code exists for the same phone number at the same time by either
  rejecting a new request or invalidating the previous active code before
  issuing a new one.
- **FR-015**: The one-time discount code MUST be displayed to the customer as a
  barcode that is readable on mobile screens and practical for cashier scanning.
- **FR-016**: The web-generated barcode SHOULD use a format distinguishable from
  EAN13, with Code 128 preferred if scanner and main application compatibility
  are confirmed.
- **FR-017**: The generated barcode value SHOULD use a recognizable format or
  prefix that allows the main restaurant application to route it to web-code
  validation instead of the standard EAN13 discount card flow.
- **FR-018**: The exact web-generated barcode format and value pattern MUST be
  verified with real scanners and the main restaurant application before final
  implementation is accepted.
- **FR-019**: The main restaurant application MUST continue its existing process
  when a scanned value is a standard EAN13 discount card barcode.
- **FR-020**: When a scanned value is a web-generated one-time code, the main
  restaurant application MUST request validation from the discount verification
  service.
- **FR-021**: The discount verification service MUST return a failed validation
  result for invalid, expired, already used, or unknown one-time codes.
- **FR-022**: The discount verification service MUST return the phone number or
  another agreed lookup key only when the one-time code is valid.
- **FR-023**: After successful validation, the one-time code MUST be immediately
  deleted or invalidated and MUST NOT be usable again.
- **FR-024**: A successfully validated one-time code MUST NOT be restored if the
  restaurant sale is later cancelled.
- **FR-025**: Sale cancellation after discount calculation MUST remain outside
  the responsibility of the discount verification service.
- **FR-026**: The main restaurant application MUST remain responsible for finding
  the discount card and calculating the discount amount.
- **FR-027**: The discount verification service MUST NOT calculate the discount
  amount.
- **FR-028**: The system MUST log important business events from customer profile
  creation through SMS verification, barcode issue, validation, failure,
  expiration, and invalidation.
- **FR-029**: Each discount redemption flow MUST have a random correlation id
  that is not the raw phone number.
- **FR-030**: The system SHOULD store a phone hash to support troubleshooting by
  phone number without exposing the raw phone number in logs.
- **FR-031**: SMS verification codes MUST expire and MUST NOT remain usable after
  expiration.
- **FR-032**: Expired one-time codes MUST be removed from active code storage.
- **FR-033**: Consumed one-time codes MUST be removed from active code storage
  immediately after successful validation.
- **FR-034**: Audit events MUST remain available for troubleshooting, support,
  and fraud investigation instead of being deleted immediately.
- **FR-035**: Frontend forms and controls MUST target WCAG AA accessibility,
  including clear labels, validation messages, focus behavior, keyboard
  accessibility, suitable mobile touch targets, sufficient contrast, and
  non-color-only status communication.
- **FR-036**: The initial release MUST exclude SMS verification during
  administrator profile creation, profile drafts, profile synchronization
  states, long-lived coupons, manual cancellation state for one-time codes,
  reservation/redeem lifecycle for barcode codes, restoring barcodes after sale
  cancellation, advanced administrator roles, and detailed profile change
  history.

### Key Entities *(include if feature involves data)*

- **Customer Profile**: A saved record that represents a customer pre-approved
  for a discount. It includes at least a phone number and profile data entered
  by an administrator. In the initial release, it has no complex workflow state.
- **SMS Verification**: A short-lived verification challenge sent to the
  customer-entered phone number during discount redemption.
- **One-Time Discount Code**: A random, short-lived, single-use code generated
  only after successful SMS verification and shown as a barcode.
- **Barcode Validation Result**: The result returned to the main restaurant
  application when it validates a web-generated one-time code, including either
  failure or an agreed lookup key.
- **Audit Event**: A trace record for important profile, verification, barcode,
  and validation events, associated with a correlation id and optional phone
  hash.
- **Correlation Id**: A random identifier for tracing one discount redemption
  flow without using the raw phone number.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 95% of eligible customers can complete the public
  redemption flow on a mobile screen in under 90 seconds after receiving the SMS
  code.
- **SC-002**: 100% of redemption attempts for phone numbers without saved
  customer profiles stop before SMS sending and barcode issuance.
- **SC-003**: 100% of successfully validated one-time codes fail when validated a
  second time.
- **SC-004**: 100% of expired, unknown, invalid, or already used one-time codes
  return failed validation results.
- **SC-005**: 100% of successful web-code validations return only the agreed
  discount-card lookup key and do not calculate or return a discount amount.
- **SC-006**: Customer-facing mobile screens meet WCAG AA accessibility checks
  for labels, focus behavior, contrast, keyboard access, touch target usability,
  and non-color-only state communication.
- **SC-007**: Barcode compatibility testing confirms the selected web-generated
  barcode format and value pattern can be scanned and distinguished from EAN13
  before production release.
- **SC-008**: 100% of redemption flows from SMS request to terminal barcode state
  can be traced by correlation id without using the raw phone number as the
  correlation id.

## Assumptions

- Customers have access to the phone number they enter during redemption and can
  receive SMS messages at payment time.
- The public customer redemption flow is used primarily on mobile devices;
  desktop support is secondary.
- Administrators are restaurant employees or managers with access to a protected
  administrative area.
- The exact administrator authorization mechanism is outside this feature
  specification and will be selected during design without changing the
  business flow.
- The final customer profile field list is not fixed yet, but phone number is
  mandatory for the initial release.
- The main restaurant application can distinguish standard EAN13 discount cards
  from web-generated one-time codes after the final barcode format is agreed.
- The main restaurant application remains the owner of discount card lookup,
  discount calculation, and sale cancellation behavior.
- The selected policy for a new code request while another code is active may be
  either rejection or invalidation of the previous active code, provided only one
  active code remains for the phone number.
- Audit event retention duration is not defined in the initial release, but
  audit events are retained long enough for troubleshooting, support, and fraud
  investigation.
