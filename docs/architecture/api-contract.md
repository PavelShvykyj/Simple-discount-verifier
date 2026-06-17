# API Contract

This document fixes the approved MVP API shape for the discount verification
system. It is intended to be detailed enough to create backend and frontend
implementation tasks without losing request parameters, response models, and
business meaning.

Storage implementation details are fixed in
`docs/architecture/table-storage-design.md`.

## Principles

- The Angular/Ionic SPA calls same-origin Azure Static Web Apps managed
  Functions under `/api`.
- Public customer APIs live under `/api/public/*` and allow anonymous access.
- Admin APIs live under `/api/admin/*` and require the Static Web Apps custom
  `admin` role.
- POS server-to-server APIs live under `/api/pos/*`, allow anonymous edge
  access, and must validate HMAC authentication inside the Azure Function.
- Scanner compatibility survey remains at the already implemented
  `/api/scanner-survey` route and requires the `admin` role.
- The discount verification service never calculates a discount amount. It only
  proves that a scanned web-code belongs to a customer who verified access to a
  saved phone number.
- Customer profiles are not synchronized to the POS in advance. If the POS
  validates a web-code, receives a phone lookup key, and then cannot find a
  local discount card by that phone, the POS may request the saved customer
  profile from this service on demand.
- `correlationId` is created when a public redemption attempt starts, before
  customer profile lookup and before SMS sending. This allows tracing both
  successful and failed attempts, including attempts where no profile exists.
- Raw phone numbers must not be used as `correlationId`. Audit and diagnostics
  should use `correlationId` and `phoneHash`; successful POS validation may
  return the raw phone number as the agreed lookup key.

## Customer Profile Shape

- One phone number represents one person and one customer profile.
- `phone` is a system field. It is stored separately because it is used for
  uniqueness, SMS verification, lookup, and `phoneHash`.
- The profile questionnaire is not versioned in the MVP.
- The profile questionnaire does not store or expose field `type`.
- The current questionnaire fields are stable and approved for the MVP:
  - `fullName`: `ФИО`
  - `birthDate`: `День рождения`
  - `favoriteDish`: `Любимое блюдо`
- Questionnaire values are stored and exchanged as `answers[]` items with
  `code`, `name`, and `value`.
- Admin create/update requests send only `code` and `value`. The backend owns
  the allowed field list and fills `name` in responses.
- Backend validation rules for the current fields:
  - `fullName` is a required string.
  - `birthDate` is optional and, when present, uses `YYYY-MM-DD`.
  - `favoriteDish` is optional free text.
  - unknown answer `code` values are rejected.

## Approved MVP Endpoints

```text
POST /api/public/redemptions
POST /api/public/redemptions/{redemptionKey}/sms-verifications

POST /api/admin/customer-profiles
GET  /api/admin/customer-profiles
GET  /api/admin/customer-profiles/by-phone/{phone}
PATCH /api/admin/customer-profiles/by-phone/{phone}
POST /api/admin/redemptions/inspect

POST /api/pos/barcodes/validate
POST /api/pos/customer-profiles/lookup

GET  /api/admin/audit-events
GET  /api/system/health

POST /api/scanner-survey
```

Endpoints intentionally not included in the MVP:

- `POST /api/public/redemptions/{redemptionKey}/barcode`: a new barcode should
  not be issued without completing a new SMS verification flow.
- `DELETE /api/admin/customer-profiles/by-phone/{phone}`: deleting profiles would
  introduce an additional business state that is not yet specified.
- `POST /api/admin/redemptions/force-approve`: manager force approval is a future
  idea and is not implemented in the current scope.
- `GET /api/system/config/public`: barcode TTL and format can be returned by
  the successful SMS verification response.

## Public Customer API

### Start Redemption

```http
POST /api/public/redemptions
Content-Type: application/json
```

Starts a customer discount redemption attempt.

Backend behavior:

1. Create `correlationId` as soon as the request reaches business handling.
2. Store audit event `redemption_started` with `correlationId`.
3. Normalize and validate the phone number. If normalization succeeds, include
   `phoneHash` in subsequent audit events for this attempt.
4. Search for an existing saved customer profile by normalized phone.
5. If no profile exists, store `profile_not_found` and stop the process without
   sending SMS and without
   issuing a barcode.
6. If a profile exists, derive the opaque phone runtime key and upsert the
   current runtime row for this phone.
7. Create a new SMS verification challenge and send SMS to the same phone
   number. This overwrites any previous current SMS challenge or active barcode
   for the same phone.
8. Enforce SMS request throttling: no more than one SMS request per 5 seconds
   for the same phone/current runtime row.

Request body:

```json
{
  "phone": "+380501234567"
}
```

Fields:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `phone` | string | yes | Customer-entered phone number. Must be accepted and normalized as a Ukrainian phone number. |

Success response:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "redemptionKey": "p_7K3F9Q2M",
  "correlationId": "c_01hxyz",
  "smsSent": true,
  "retryAfterSeconds": 5,
  "smsExpiresAt": "2026-06-16T14:03:00Z"
}
```

Response fields:

| Field | Type | Description |
| --- | --- | --- |
| `redemptionKey` | string | Public flow key used by the frontend for the next SMS verification request. It is the opaque phone runtime key, not a raw phone number. A new flow for the same phone returns the same key but replaces the current runtime data. |
| `correlationId` | string | Trace identifier for this redemption attempt. The public frontend must keep it immediately after the first response and use it for the admin-support QR button even before any barcode exists. |
| `smsSent` | boolean | `true` when the SMS send request was accepted. |
| `retryAfterSeconds` | number | Minimum delay before another SMS request can be attempted for this flow. |
| `smsExpiresAt` | string | ISO 8601 UTC timestamp when the SMS code expires. |

Profile not found response:

```http
HTTP/1.1 404 Not Found
Content-Type: application/json
```

```json
{
  "error": {
    "code": "profile_not_found",
    "message": "Profile was not found for this phone.",
    "correlationId": "c_01hxyz"
  }
}
```

Other expected errors:

- `400 invalid_phone`: phone is missing or cannot be normalized to an accepted
  Ukrainian phone format. Because `correlationId` is created before phone
  normalization, the error response should include it unless the request failed
  before business handling started, for example unreadable JSON or an
  infrastructure failure.
- `429 sms_retry_too_soon`: repeated SMS request is blocked by the 5 second
  limit. The error response includes `correlationId`.
- `502 sms_send_failed`: SMS provider failed or did not accept the message; the
  frontend should show a retryable error. The error response includes
  `correlationId`.

### Verify SMS

```http
POST /api/public/redemptions/{redemptionKey}/sms-verifications
Content-Type: application/json
```

Verifies the SMS code for an existing redemption. If the code is valid, the
backend immediately issues a short-lived one-time barcode value.

Backend behavior:

1. Load the current phone runtime row by `redemptionKey`.
2. Reject missing, expired, already failed, or already completed challenges.
3. Validate the submitted SMS code.
4. Reject the challenge after 3 invalid attempts for one SMS code.
5. If the code is valid, mark the phone verified for this redemption.
6. Generate a new random one-time barcode value containing the opaque phone
   runtime key.
7. Store only its `BarcodeHash` in the current runtime row with a default
   configurable TTL of 3 minutes.
9. Store audit events for failed SMS validation, phone verification, barcode
   invalidation, and barcode issuance as applicable.

Request body:

```json
{
  "code": "123456"
}
```

Fields:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | SMS code entered by the customer. |

Success response:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "correlationId": "c_01hxyz",
  "barcodeValue": "SDV-p_7K3F9Q2M-c_01hxyz",
  "barcodeFormat": "code128",
  "expiresAt": "2026-06-16T14:06:00Z",
  "ttlSeconds": 180
}
```

Response fields:

| Field | Type | Description |
| --- | --- | --- |
| `correlationId` | string | Trace identifier for this redemption attempt. |
| `barcodeValue` | string | Value to render as a barcode and later send by the POS to the validation API. Format: `SDV-<phoneRuntimeKey>-<correlationId>`. The value contains the opaque phone runtime key for runtime lookup and the correlation id for fast support/audit lookup. The backend stores only a hash of the full value. |
| `barcodeFormat` | string | Barcode rendering format. MVP value: `code128`. |
| `expiresAt` | string | ISO 8601 UTC timestamp when the barcode expires. |
| `ttlSeconds` | number | Barcode lifetime in seconds. Default MVP value: `180`, configurable server-side. |

Expected errors:

- `400 invalid_request`: `code` is missing or malformed.
- `404 redemption_not_found`: no current runtime row exists for `redemptionKey`.
- `410 sms_expired`: the SMS code expired.
- `422 invalid_sms_code`: the SMS code is incorrect but attempts remain.
- `423 sms_attempts_exceeded`: 3 invalid attempts were reached and no barcode
  must be issued.
- `409 redemption_already_completed`: the current runtime row already produced
  a barcode for this SMS challenge; the customer must start a new flow to get a
  new SMS challenge and barcode.

## Admin API

Admin APIs require the Static Web Apps custom `admin` role. They must not rely
on the broad `authenticated` role.

### Create Customer Profile

```http
POST /api/admin/customer-profiles
Content-Type: application/json
```

Creates a saved customer profile that makes the customer eligible to start the
discount redemption flow.

Request body:

```json
{
  "phone": "+380501234567",
  "answers": [
    {
      "code": "fullName",
      "value": "Ivan Petrenko"
    },
    {
      "code": "birthDate",
      "value": "1990-04-15"
    },
    {
      "code": "favoriteDish",
      "value": "Pizza Margherita"
    }
  ]
}
```

Fields:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `phone` | string | yes | Customer phone number. Must be accepted and normalized as a Ukrainian phone number. |
| `answers` | array | yes | Questionnaire answers. |
| `answers[].code` | string | yes | One of the allowed questionnaire field codes: `fullName`, `birthDate`, `favoriteDish`. |
| `answers[].value` | string/null | yes | Answer value. Empty optional values may be omitted or sent as `null`. |

Success response:

```http
HTTP/1.1 201 Created
Content-Type: application/json
```

```json
{
  "phone": "+380501234567",
  "answers": [
    {
      "code": "fullName",
      "name": "ФИО",
      "value": "Ivan Petrenko"
    },
    {
      "code": "birthDate",
      "name": "День рождения",
      "value": "1990-04-15"
    },
    {
      "code": "favoriteDish",
      "name": "Любимое блюдо",
      "value": "Pizza Margherita"
    }
  ],
  "createdAt": "2026-06-16T13:55:00Z",
  "updatedAt": "2026-06-16T13:55:00Z"
}
```

Expected errors:

- `400 invalid_phone`: phone is missing or invalid.
- `400 invalid_profile_answers`: required answers are missing, an answer code
  is unknown, or an answer value has an invalid format.
- `409 duplicate_profile`: a profile already exists for the normalized phone.

Notes:

- No SMS verification is required during profile creation in the MVP.
- No customer profile workflow status is stored in the MVP. A profile exists
  and is saved, or it does not exist.
- The backend stores one profile per normalized phone number.
- The normalized phone key is the storage identity of the profile. The MVP does
  not create a separate `profileId`.
- The backend does not store questionnaire versions or answer types.

### List Customer Profiles

```http
GET /api/admin/customer-profiles?phone=+380501234567&pageSize=50&continuationToken=...
```

Lists customer profiles and supports lookup by phone.

Query parameters:

| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| `phone` | string | no | Optional phone filter. Backend normalizes the value and performs a point lookup by normalized phone key. |
| `pageSize` | number | no | Maximum records to return. Default and maximum should be defined during implementation. |
| `continuationToken` | string | no | Opaque token for fetching the next page from storage. |

Success response:

```json
{
  "items": [
    {
      "phone": "+380501234567",
      "answers": [
        {
          "code": "fullName",
          "name": "ФИО",
          "value": "Ivan Petrenko"
        },
        {
          "code": "birthDate",
          "name": "День рождения",
          "value": "1990-04-15"
        },
        {
          "code": "favoriteDish",
          "name": "Любимое блюдо",
          "value": "Pizza Margherita"
        }
      ],
      "createdAt": "2026-06-16T13:55:00Z",
      "updatedAt": "2026-06-16T13:55:00Z"
    }
  ],
  "continuationToken": null
}
```

### Get Customer Profile

```http
GET /api/admin/customer-profiles/by-phone/{phone}
```

The `phone` path value must be URL-encoded when it contains `+`.

Success response:

```json
{
  "phone": "+380501234567",
  "answers": [
    {
      "code": "fullName",
      "name": "ФИО",
      "value": "Ivan Petrenko"
    },
    {
      "code": "birthDate",
      "name": "День рождения",
      "value": "1990-04-15"
    },
    {
      "code": "favoriteDish",
      "name": "Любимое блюдо",
      "value": "Pizza Margherita"
    }
  ],
  "createdAt": "2026-06-16T13:55:00Z",
  "updatedAt": "2026-06-16T13:55:00Z"
}
```

Expected error:

- `404 profile_not_found`: profile does not exist for the phone.

### Update Customer Profile

```http
PATCH /api/admin/customer-profiles/by-phone/{phone}
Content-Type: application/json
```

Updates editable profile fields for the current phone in the path. The `phone`
path value must be URL-encoded when it contains `+`.

If the request body contains a different phone, the backend treats it as a phone
change: normalize and validate the new phone, ensure the new phone key is not
already used, create the new row, and remove the old row using ETag-aware
operations.

Request body:

```json
{
  "phone": "+380501234567",
  "answers": [
    {
      "code": "fullName",
      "value": "Ivan Petrenko"
    },
    {
      "code": "birthDate",
      "value": "1990-04-15"
    },
    {
      "code": "favoriteDish",
      "value": "Pizza Margherita"
    }
  ]
}
```

Success response:

```json
{
  "phone": "+380501234567",
  "answers": [
    {
      "code": "fullName",
      "name": "ФИО",
      "value": "Ivan Petrenko"
    },
    {
      "code": "birthDate",
      "name": "День рождения",
      "value": "1990-04-15"
    },
    {
      "code": "favoriteDish",
      "name": "Любимое блюдо",
      "value": "Pizza Margherita"
    }
  ],
  "createdAt": "2026-06-16T13:55:00Z",
  "updatedAt": "2026-06-16T14:10:00Z"
}
```

Expected errors:

- `400 invalid_phone`: supplied phone is invalid.
- `400 invalid_profile_answers`: required answers are missing, an answer code
  is unknown, or an answer value has an invalid format.
- `404 profile_not_found`: profile does not exist for the path phone.
- `409 duplicate_profile`: another profile already uses the normalized phone.

### Inspect Redemption

```http
POST /api/admin/redemptions/inspect
Content-Type: application/json
```

Shows support information for a specific redemption attempt. This endpoint is
for administrator troubleshooting at the restaurant. It does not approve,
extend, consume, restore, or otherwise change barcode validity.

The preferred lookup key is `correlationId`, because problems can happen before
the barcode is created. The public frontend receives `correlationId` from the
first `POST /api/public/redemptions` response whenever the backend can create
one. A special frontend button should generate an admin/support QR from this
`correlationId` so the administrator can open the troubleshooting view even if
SMS sending, SMS verification, or barcode generation failed.

If a barcode already exists, the admin frontend may also submit `barcodeValue`;
the backend parses the embedded `correlationId` and uses the same inspection
flow.

Request body:

```json
{
  "correlationId": "c_01hxyz",
  "barcodeValue": "SDV-p_7K3F9Q2M-c_01hxyz"
}
```

At least one of `correlationId` or `barcodeValue` is required. If both are
provided, they must refer to the same correlation id.

Response:

```json
{
  "correlationId": "c_01hxyz",
  "redemption": {
    "status": "barcode_consumed",
    "startedAt": "2026-06-16T14:00:00Z",
    "lastEventAt": "2026-06-16T14:04:12Z"
  },
  "barcode": {
    "value": "SDV-p_7K3F9Q2M-c_01hxyz",
    "formatValid": true,
    "phoneRuntimeKey": "p_7K3F9Q2M",
    "correlationId": "c_01hxyz",
    "status": "consumed",
    "expiresAt": "2026-06-16T14:06:00Z",
    "consumedAt": "2026-06-16T14:04:12Z",
    "consumedByScanId": "sale-20260616-000123"
  },
  "scan": {
    "scanId": "sale-20260616-000123",
    "parsed": {
      "raw": "sale-20260616-000123"
    }
  },
  "profile": {
    "phone": "+380501234567",
    "answers": [
      {
        "code": "fullName",
        "name": "ФИО",
        "value": "Ivan Petrenko"
      },
      {
        "code": "birthDate",
        "name": "День рождения",
        "value": "1990-04-15"
      },
      {
        "code": "favoriteDish",
        "name": "Любимое блюдо",
        "value": "Pizza Margherita"
      }
    ]
  },
  "auditEvents": [
    {
      "eventType": "barcode_validation_succeeded",
      "occurredAt": "2026-06-16T14:04:12Z",
      "actorType": "pos",
      "actorId": "main-pos-system",
      "metadata": {
        "scanId": "sale-20260616-000123"
      }
    }
  ]
}
```

Redemption `status` values:

| Status | Meaning |
| --- | --- |
| `started` | The first public redemption request was accepted and the attempt was created. |
| `profile_not_found` | No customer profile exists for the entered phone. |
| `sms_send_failed` | SMS provider failed or did not accept the message. |
| `sms_sent` | SMS was sent and is waiting for customer input. |
| `sms_failed` | SMS verification failed or the attempt limit was exceeded. |
| `barcode_issued` | SMS verification succeeded and a barcode was issued. |
| `barcode_consumed` | POS validation consumed the barcode successfully. |
| `barcode_expired` | Barcode was issued but expired before successful POS validation. |
| `unknown` | No audit/runtime data can be found for the supplied identifier. |

Barcode `status` values:

| Status | Meaning |
| --- | --- |
| `active` | Barcode is current, not expired, and not consumed. |
| `expired` | Barcode belongs to the current runtime row but its TTL expired. |
| `consumed` | Barcode was consumed; `consumedByScanId` shows the POS scan/sale id when available. |
| `replaced_by_new_flow` | Barcode has a valid `correlationId`, but the phone runtime row now belongs to a newer flow. |
| `invalid_format` | Barcode cannot be parsed as a web barcode. |
| `not_issued` | The redemption attempt exists, but no barcode has been issued yet. |
| `unknown` | Barcode/runtime data cannot be found. |

Notes:

- `scan.parsed` is intentionally flexible. The POS-owned `scanId` may encode
  hall, table, order, branch, or other local context. The backend may parse it
  later when the POS `scanId` convention is known.
- Manager force approval or unconditional barcode permission is not part of the
  current scope. It may be considered in a future phase, but the current admin
  endpoint is read-only.

## POS API

### Validate Barcode

```http
POST /api/pos/barcodes/validate
Content-Type: application/json
x-client-id: main-pos-system
x-timestamp: 2026-06-16T14:04:00Z
x-signature: <base64-hmac-sha256>
```

Validates a scanned web-generated barcode value for the main restaurant
application. The POS should call this endpoint only for barcode values that
match the agreed web-code prefix/pattern. Standard EAN13 discount cards must
continue through the existing POS flow without calling this API.

Authentication:

- `x-client-id` is required. Initial value: `main-pos-system`.
- `x-timestamp` is required and must be fresh according to server-side
  tolerance.
- `x-signature` is required.
- The HMAC secret is stored only in server-side configuration and must never be
  exposed to Angular or browser-delivered assets.
- Nonce-based replay protection is out of scope for the MVP.

Recommended canonical string for HMAC:

```text
METHOD
PATH
X-TIMESTAMP
SHA256_HEX(BODY)
```

Example canonical string:

```text
POST
/api/pos/barcodes/validate
2026-06-16T14:04:00Z
<request-body-sha256-hex>
```

Request body:

```json
{
  "barcodeValue": "SDV-p_7K3F9Q2M-c_01hxyz",
  "terminalId": "POS-01",
  "branchId": "kyiv-obolon",
  "scanId": "sale-20260616-000123"
}
```

Fields:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `barcodeValue` | string | yes | Scanned web-code value generated by the public redemption flow. |
| `terminalId` | string | no | POS terminal/workplace identifier for audit. |
| `branchId` | string | no | Restaurant branch identifier for audit. |
| `scanId` | string | yes | POS-side unique identifier of the scan/sale operation. POS must reuse the same `scanId` when retrying the same validation request. |

Business validation success:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "valid": true,
  "lookupKeyType": "phone",
  "lookupKey": "+380501234567",
  "correlationId": "c_01hxyz",
  "validatedAt": "2026-06-16T14:04:12Z",
  "idempotentReplay": false
}
```

Fields:

| Field | Type | Description |
| --- | --- | --- |
| `valid` | boolean | `true` when the barcode was accepted. |
| `lookupKeyType` | string | MVP value: `phone`. |
| `lookupKey` | string | Verified phone number used by the main restaurant application to find the discount card. |
| `correlationId` | string | Trace identifier for the original redemption flow. |
| `validatedAt` | string | ISO 8601 UTC timestamp when validation completed. |
| `idempotentReplay` | boolean | `false` for the first successful consumption; `true` when the same consumed barcode is retried with the same `scanId`. |

Side effect:

- After this response is produced, the one-time barcode must be immediately
  deleted or invalidated. A second validation attempt for the same value must
  fail unless it is an idempotent retry with the same `scanId`.

Idempotent replay response for the same consumed barcode and the same `scanId`:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "valid": true,
  "lookupKeyType": "phone",
  "lookupKey": "+380501234567",
  "correlationId": "c_01hxyz",
  "validatedAt": "2026-06-16T14:04:12Z",
  "idempotentReplay": true
}
```

The POS must use the same `scanId` when retrying the same validation request
after timeout or network failure. If the barcode was already consumed by the
same `scanId`, the backend returns the same successful business result instead
of `already_used`.

Business validation failure:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "valid": false,
  "reason": "expired",
  "correlationId": "c_01hxyz",
  "validatedAt": "2026-06-16T14:04:12Z"
}
```

`reason` values:

| Reason | Meaning |
| --- | --- |
| `unknown` | No active or known barcode record exists for this value. `correlationId` may be `null`. |
| `expired` | The barcode existed but its TTL expired. |
| `already_used` | The barcode was already consumed by a previous successful validation. |
| `invalid_format` | The value does not match the agreed web-code prefix or format. |

Failure response type:

```json
{
  "valid": false,
  "reason": "unknown",
  "correlationId": null,
  "validatedAt": "2026-06-16T14:04:12Z"
}
```

Transport/authentication errors:

- `400 invalid_request`: request body is invalid, `barcodeValue` is missing, or
  `scanId` is missing/malformed.
- `401 unauthorized`: HMAC headers are missing, stale, unknown, or invalid.
- `500 internal_error`: unexpected server-side failure.

For POS integration, HTTP `200` means the request was authenticated and the
barcode was evaluated. The POS should then use `valid`. Non-`200` responses mean
an integration, request, authentication, or infrastructure problem.

### Get Customer Profile By Phone

```http
POST /api/pos/customer-profiles/lookup
Content-Type: application/json
x-client-id: main-pos-system
x-timestamp: 2026-06-16T14:04:20Z
x-signature: <base64-hmac-sha256>
```

Returns the saved customer profile to the POS on demand.

This endpoint is not a background synchronization API. The POS may call it when
it needs to fetch a saved profile by phone, most commonly after this sequence:

1. POS validates a scanned web-code through
   `POST /api/pos/barcodes/validate`.
2. The validation response is `valid: true` and returns
   `lookupKeyType: "phone"` with `lookupKey`.
3. POS searches its local discount card storage by that phone.
4. POS does not find a local discount card/customer questionnaire and needs the
   saved profile data from this service to create or complete the local record.

Authentication is the same HMAC scheme as
`POST /api/pos/barcodes/validate`.

Request body:

```json
{
  "phone": "+380501234567",
  "terminalId": "POS-01",
  "branchId": "kyiv-obolon"
}
```

Fields:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `phone` | string | yes | Phone lookup key returned by successful barcode validation. |
| `terminalId` | string | no | POS terminal/workplace identifier for audit. |
| `branchId` | string | no | Restaurant branch identifier for audit. |

Backend behavior:

1. Validate HMAC headers.
2. Normalize and validate `phone`.
3. Return the saved profile if it exists.
4. Store audit events for requested, returned, or not-found outcomes.

Success response:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "found": true,
  "profile": {
    "phone": "+380501234567",
    "answers": [
      {
        "code": "fullName",
        "name": "ФИО",
        "value": "Ivan Petrenko"
      },
      {
        "code": "birthDate",
        "name": "День рождения",
        "value": "1990-04-15"
      },
      {
        "code": "favoriteDish",
        "name": "Любимое блюдо",
        "value": "Pizza Margherita"
      }
    ]
  },
  "servedAt": "2026-06-16T14:04:20Z"
}
```

Response fields:

| Field | Type | Description |
| --- | --- | --- |
| `found` | boolean | `true` when a saved profile exists for the phone. |
| `profile.phone` | string | Normalized phone number. |
| `profile.answers` | array | Questionnaire answers in POS-compatible `code`, `name`, `value` form. |
| `profile.answers[].code` | string | Stable answer code. |
| `profile.answers[].name` | string | Human-readable questionnaire item name. |
| `profile.answers[].value` | string/null | Stored answer value. |
| `servedAt` | string | ISO 8601 UTC timestamp when the profile was returned. |

Not found response:

```http
HTTP/1.1 404 Not Found
Content-Type: application/json
```

```json
{
  "error": {
    "code": "profile_not_found",
    "message": "Profile was not found for this phone."
  }
}
```

Expected errors:

- `400 invalid_phone`: phone is missing or cannot be normalized.
- `401 unauthorized`: HMAC headers are missing, stale, unknown, or invalid.
- `404 profile_not_found`: no saved profile exists for the phone.

Audit:

- Store `pos_profile_requested` for every authenticated request.
- Store `pos_profile_returned` when a profile is returned.
- Store `pos_profile_not_found` when the profile is not found.
- Audit records should include `phoneHash`, `actorType: "pos"`, `actorId`,
  and available POS metadata from headers or future request parameters.

## Audit API

### List Audit Events

```http
GET /api/admin/audit-events?correlationId=c_01hxyz&phone=+380501234567&pageSize=50&continuationToken=...
```

Returns fraud-relevant and support-relevant events. Admin-only.

Query parameters:

| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| `correlationId` | string | no | Filter events by one redemption flow. |
| `phone` | string | no | Optional support lookup. Backend normalizes and hashes the phone, then searches by `phoneHash`. |
| `pageSize` | number | no | Maximum records to return. |
| `continuationToken` | string | no | Opaque token for fetching the next page from storage. |

Success response:

```json
{
  "items": [
    {
      "id": "ae_01hxyz",
      "correlationId": "c_01hxyz",
      "eventType": "barcode_validation_succeeded",
      "phoneHash": "sha256:...",
      "occurredAt": "2026-06-16T14:04:12Z",
      "actorType": "pos",
      "actorId": "main-pos-system",
      "metadata": {
        "terminalId": "POS-01",
        "branchId": "kyiv-obolon",
        "scanId": "sale-20260616-000123"
      }
    }
  ],
  "continuationToken": null
}
```

Recommended event types:

- `customer_profile_created`
- `customer_profile_updated`
- `redemption_started`
- `invalid_phone`
- `profile_not_found`
- `profile_found`
- `sms_send_requested`
- `sms_sent`
- `sms_send_failed`
- `sms_validation_failed`
- `phone_verified`
- `previous_barcode_invalidated`
- `barcode_issued`
- `barcode_validation_requested`
- `barcode_validation_failed`
- `barcode_validation_succeeded`
- `barcode_validation_idempotent_replay`
- `barcode_consumed`
- `barcode_expired`
- `pos_profile_requested`
- `pos_profile_returned`
- `pos_profile_not_found`

## System API

### Health

```http
GET /api/system/health
```

Success response:

```json
{
  "status": "ok",
  "service": "simple-discount-verifier-api"
}
```

This endpoint should not expose secrets, connection strings, HMAC configuration,
or SMS provider details.

## Scanner Survey API

The scanner survey endpoint already exists and remains part of the MVP as a
temporary admin tool for Code 128, QR, scanner, and browser-camera compatibility
checks.

The endpoint contract does not need a new shape for QR checks. The frontend
records each tested sample as `barcodeId`, `isReadable`, and `comment`.
The `scanner-survey` page must also allow checking whether a mobile browser can
read a QR code or test barcode displayed by another instance of the app. For the
Angular implementation use `@zxing-js/ngx-scanner`, the Angular integration
recommended from the `@zxing/browser` package documentation.

```http
POST /api/scanner-survey
Content-Type: application/json
```

Request body:

```json
{
  "branchName": "Pizza Center Obolon",
  "submittedAtClient": "2026-06-16T14:30:00.000Z",
  "terminals": [
    {
      "terminalName": "POS-01",
      "answers": [
        {
          "barcodeId": "code128-web-prefix-short",
          "isReadable": true,
          "comment": ""
        },
        {
          "barcodeId": "qr-admin-inspect-cross-instance-zxing",
          "isReadable": true,
          "comment": "Read by mobile browser from another app instance"
        }
      ]
    }
  ],
  "comment": "Branch-wide note"
}
```

Success response:

```json
{
  "submissionId": "4f5a...",
  "rowsWritten": 1
}
```

Invalid JSON returns `400 Bad Request`.

Suggested `barcodeId` values for the current survey:

| `barcodeId` | Purpose |
| --- | --- |
| `code128-web-prefix-short` | Short Code 128 web-code sample with the `SDV-` prefix. |
| `code128-web-prefix-current-format` | Code 128 sample using `SDV-<phoneRuntimeKey>-<correlationId>`. |
| `ean13-valid-discount-card` | Existing EAN13 discount-card control sample. |
| `qr-admin-inspect-cross-instance-zxing` | QR shown in one app instance and read by another mobile browser instance through `@zxing-js/ngx-scanner`. |

## Common Error Model

For non-POS business validation errors and all request/authentication failures,
use this shape:

```json
{
  "error": {
    "code": "invalid_request",
    "message": "Human-readable message.",
    "correlationId": "c_01hxyz"
  }
}
```

`correlationId` may be omitted or `null` only when the request failed before a
correlation id could be created or resolved.

Common HTTP meanings:

| Status | Meaning |
| --- | --- |
| `200` | Request accepted and completed. For POS validation, inspect `valid`. |
| `201` | Resource created. |
| `400` | Missing or invalid input. |
| `401` | Authentication failed. |
| `403` | Authenticated user does not have the required role. |
| `404` | Requested resource or required profile was not found. |
| `409` | Conflict, usually duplicate profile or already completed flow. |
| `410` | Expired temporary challenge/code. |
| `422` | Business validation failed while the flow can still continue. |
| `423` | Flow is locked/failed because the SMS attempt limit was exceeded. |
| `429` | Rate limit or retry interval violation. |
| `500` | Unexpected server-side error. |
| `502` | External provider failure, such as SMS send failure. |

## Frontend Implementation Notes

- Use Angular `HttpClient` for all API calls.
- The public frontend flow has only two backend calls:
  1. `POST /api/public/redemptions`
  2. `POST /api/public/redemptions/{redemptionKey}/sms-verifications`
- The frontend renders `barcodeValue` using `barcodeFormat = "code128"`.
- If SMS verification succeeds, the frontend does not call a separate barcode
  issuance endpoint.
- If the customer needs a new barcode after expiry or completion, start a new
  redemption flow.
- Admin frontend should use `/api/admin/customer-profiles` for profile
  management and `/api/admin/audit-events` for support/fraud investigation.
- Admin frontend should use `/api/admin/redemptions/inspect` to show support
  information for a redemption attempt by `correlationId`; `barcodeValue` is an
  optional input when a barcode already exists. This endpoint does not change
  barcode validity.
- After the first `POST /api/public/redemptions` response, the public frontend
  should keep `correlationId` even on business errors and show a special
  admin-support QR action. The QR should encode an admin route or payload based
  on `correlationId`, not on a manually typed support code.
- Admin customer profile forms should render the current fixed questionnaire
  fields from a small frontend field definition list: phone, full name, birth
  date, and favorite dish.
- The frontend sends profile answers as `code` and `value`; it does not send
  answer `name` or `type`.
- POS integration endpoints are not called by the Angular frontend.
