# Data Lifecycle

## Customer Profile

- Created by an authorized administrator.
- Stored as the source of pre-approved discount eligibility.
- Identified for integration primarily by phone number.
- Has no workflow state in the initial release.

## SMS Verification

- Created only after a customer enters a phone number that has a saved profile.
- Expires after a short configured lifetime.
- Must not remain usable after expiration.
- Fails after invalid, expired, or excessive attempts.

## One-Time Barcode Code

- Created only after successful SMS verification.
- Default lifetime is 3 minutes and must be configurable.
- Only one active code should exist for the same phone number.
- A new request must either reject issuance or invalidate the previous active code.
- Removed or invalidated immediately after successful validation.
- Expired codes are removed from active storage.
- Consumed codes are not restored after sale cancellation.

## Audit Events

- Kept for troubleshooting, support, and fraud investigation.
- Linked by `correlationId`.
- Should include `phoneHash` instead of raw phone number where possible.
