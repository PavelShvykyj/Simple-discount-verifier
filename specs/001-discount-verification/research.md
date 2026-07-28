# Research: Client-Side Phone Confirmation During Profile Creation

## Decision 1: Treat The Flow As An Operational Entry Check

**Decision**: The administrator is trusted. The goal is to detect an incorrectly
entered phone while the customer is present, not to defend the profile API
against an administrator intentionally bypassing the UI.

**Rationale**: An intentionally wrong number creates immediate operational
problems for the same administrator when the customer later requests a
discount. The agreed two-digit exchange is faster than inspecting or bypassing
the browser flow.

**Alternatives considered**:

- Server-attested OTP verification: rejected because it adds backend state and
  sends no additional business value under the agreed trust model.
- Six-digit security OTP: rejected because the flow is not being used as a
  strong authenticator.

## Decision 2: Generate And Compare The Code In The Frontend

**Decision**: Generate `00` through `99` in the create-form component, retain it
only in component memory, and compare the dictated value locally.

**Rationale**: The backend does not know whether the locally entered value
matched and does not need to. This avoids a challenge record, verification
token, expiry cleanup, verification endpoint, and changes to the profile
contract.

**Alternatives considered**:

- Backend generation: rejected because it still needs a way to associate and
  verify the challenge.
- Persisting the code in browser storage: rejected because component memory
  already covers the form lifetime.
- Shared frontend store: rejected because the confirmation belongs to one form
  instance and must disappear when that instance disappears.

## Decision 3: Use A Narrow Stateless Backoffice Endpoint

**Decision**: Add
`POST /api/backoffice/customer-profiles/activation-code-sms` accepting only
`phone` and `code`. Access is mandatory only for authenticated identities with
the Azure Static Web Apps custom role `admin`.

**Rationale**: The browser cannot call SMS-Fly directly without exposing the
provider credential. The existing `/api/backoffice/*` route rule supplies the
admin boundary and rejects anonymous or authenticated non-admin callers before
the Function is invoked. The Function may retain the existing backoffice
`AuthorizationLevel.Anonymous` trigger because SWA is the enforcing
authorization boundary. The existing `ISmsSender`/`SmsFlyClient` supplies
delivery.

**Alternatives considered**:

- Calling SMS-Fly from the browser: rejected because it would expose a secret.
- A generic arbitrary-message endpoint: rejected because it would create an
  unnecessary SMS relay; the backend must build the fixed message template.
- Sending the entire questionnaire: rejected because delivery requires only the
  phone and code.

## Decision 4: Keep The Existing Profile Contract And Storage Unchanged

**Decision**: The code, match result, and verification timestamp are not added
to `CustomerProfileUpsertRequest`, `CustomerProfileRecord`, Azure Table Storage,
or POS responses.

**Rationale**: The confirmation is complete before the one existing create
request is sent. Persisting it would falsely imply server-attested verification
and would add migration and integration work.

**Alternatives considered**:

- `phoneVerifiedAt` or `phoneVerified`: rejected because the backend never
  performs or observes the comparison.
- Pending profile state: rejected because no profile data needs to leave the
  form before confirmation.

## Decision 5: Limit The First Implementation To Create Mode

**Decision**: Existing profile editing keeps its current behavior.

**Rationale**: The agreed task concerns creation. Extending the rule to phone
changes would require a separate business decision and may affect POS lookup by
phone.

**Alternatives considered**:

- Verify all phone edits: deferred until phone-change/POS behavior is specified.
- Make phone immutable: not requested.

## Decision 6: Keep Resend Logic Local And Minimal

**Decision**: Disable duplicate sends only while a request is in flight.
Generate one code per normalized phone/form instance and reuse it for every
retry/resend until the phone or form changes.

**Rationale**: Reuse prevents an older delayed SMS from becoming misleading and
handles the case where SMS-Fly accepted a request but the browser did not
receive the response. The endpoint is admin-only and administrators are
trusted. A server rate-limit, timer, send counter, or CAPTCHA would require
state and does not serve the current threat model.

**Alternatives considered**:

- Backend cooldown/rate limit: rejected for the current trusted-admin flow.
- New countdown UI: rejected because resend timing was not requested.

## Decision 7: Reuse Existing Layers Without New Abstractions

**Decision**: Add one method to `AdminCustomerProfileService`, one Function
method to `AdminCustomerProfilesFunction`, and one method to the existing
Angular API client. Reuse the current Ionic `ion-input-otp` pattern in the
existing form rather than introducing another input component.

**Rationale**: These are the existing owners of profile administration and SMS
delivery dependencies. A new verification service, repository, store, or
component would have one caller and no independent lifecycle.

**Alternatives considered**:

- New backend service/interface: rejected as single-use boilerplate.
- Reusing public redemption runtime state: rejected because it would couple
  separate flows and could overwrite active redemption state.

## Resolved Specification Conflict

The current specification says that profile creation does not require SMS
verification. The confirmed 2026-07-28 decision supersedes that text with a
client-side confirmation guard. This is resolved user input, not an open
clarification; the implementation phase must update the stale specification
before changing code.
