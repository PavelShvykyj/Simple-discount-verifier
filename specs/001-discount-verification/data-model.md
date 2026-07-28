# Data Model: Client-Side Phone Confirmation

## Persisted Model

No persisted model changes.

`CustomerProfile` remains:

- `phone`
- `physicalCardNumber`
- `answers`
- `createdAt`
- `updatedAt`

The profile request does not contain the SMS code or a verification field.
Azure Table `CustomerProfiles`, `DiscountRuntime`, and `AuditEvents` schemas are
unchanged.

## Ephemeral Frontend State

The create-form component owns the following runtime values:

| Value | Type | Meaning |
| --- | --- | --- |
| `sentPhone` | normalized phone or `null` | Phone used in the last accepted SMS-send request |
| `expectedCode` | two-character string or `null` | Code generated once for the current phone, including before a failed first send |
| `enteredCode` | form-control string | Value dictated by the customer and entered by the administrator |
| `isSendingCode` | boolean | Whether the SMS request is in flight |
| `isCodeMatched` | derived boolean | Current phone equals `sentPhone` and `enteredCode` exactly equals `expectedCode` |

`expectedCode` must preserve leading zeroes. It is never parsed as a number.

## Validation Rules

### Send request

- `phone` must normalize through the existing Ukrainian phone rules.
- `code` must be exactly two ASCII decimal characters: `[0-9]{2}`.
- No questionnaire or physical-card data is sent.

### Local match

- Comparison is exact string equality.
- Matching is valid only for the exact normalized phone used by the successful
  send.
- A mismatch does not increment a counter or call the backend.

### Profile creation

- Existing phone, physical-card, and questionnaire validation remains.
- Create mode additionally requires `isCodeMatched = true`.
- The API payload remains the existing `CustomerProfileUpsertRequest`.
- Edit mode does not use this state.

## State Transitions

```text
idle
  -> sending(new code)            first send requested
  -> idle                         phone edited/reset/closed

sending
  -> sent                         backend accepted SMS
  -> prepared(code retained)      first send failed
  -> sent(same code retained)     resend failed

prepared
  -> sending(same code)           retry requested
  -> idle                         phone edited/reset/closed

sent
  -> matched                      entered code equals expected code
  -> sent                         entered code differs
  -> sending(same code)           resend requested
  -> idle                         phone edited/reset/closed

matched
  -> sending(same code)           resend requested
  -> idle                         phone edited/reset/closed
  -> idle                         profile created and form reset
```

There is no backend verification state and no state transition after a direct
profile API call.
