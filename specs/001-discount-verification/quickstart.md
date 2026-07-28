# Quickstart: Profile Phone Confirmation

## Preconditions

- Use an approved test phone; the smoke scenario sends a real SMS when the
  configured SMS-Fly account is used.
- Configure the existing `SmsFlyApiKey`, `SmsFlySender`, and storage settings in
  `functions/local.settings.json`.
- Verify `SmsCodeTtlSeconds` as well; `SmsFlyClient` uses it as the provider
  delivery TTL even though this feature stores no verification TTL.
- For deployed authorization checks, prepare identities with and without the
  Azure Static Web Apps custom role `admin`.
- No new table, secret, or Azure setting is required.

## Run Locally

Start the backend:

```powershell
Set-Location functions
func start
```

Start the frontend through the existing local proxy:

```powershell
Set-Location frontend
npm run start:local-api
```

Open the administrator customer-profile create page through the authenticated
environment used for admin testing.

## Contract Smoke

Send only a phone and two-character code:

```http
POST /api/backoffice/customer-profiles/activation-code-sms
Content-Type: application/json

{
  "phone": "+380501234567",
  "code": "07"
}
```

Expected accepted response: `202 Accepted` with an empty body.

Check that the response and application logs do not contain `07`.

## Deployed Authorization Smoke

This check must run through Azure Static Web Apps; a direct local Function call
does not exercise SWA role enforcement.

1. As an authenticated `admin`, call the endpoint and confirm the request
   reaches the Function.
2. As an authenticated user without `admin`, confirm the existing SWA `403`
   handling is applied and the Function is not invoked.
3. Without authentication, confirm the existing SWA `401`/login redirect
   handling is applied and the Function is not invoked.
4. Confirm the deployed route configuration contains `/api/backoffice/*` with
   `allowedRoles: ["admin"]`.

## Manual UI Scenario

1. Open the create form.
2. Enter a valid Ukrainian phone; leave the questionnaire incomplete.
3. Send the activation code.
4. Confirm in browser network tools that the SMS call contains only `phone` and
   `code`, not questionnaire or physical-card data.
5. Enter different two digits and confirm profile creation remains disabled
   with a visible mismatch message.
6. Enter the digits received by SMS and confirm the phone-success message
   appears.
7. Complete the remaining fields and create the profile.
8. Confirm the existing profile request body is unchanged and contains no
   activation code.
9. Start another create flow, match a code, then edit the phone. Confirm the
   match disappears and the old code cannot unlock creation even after typing
   the old phone again.
10. Confirm edit mode still saves without activation-code controls.

## Automated Verification

```powershell
dotnet build functions/SimpleDiscountVerifier.Api.csproj
npm --prefix frontend test
npm --prefix frontend run lint
npm --prefix frontend run format:check
npm --prefix frontend run build
```

## Expected Non-Changes

- No Azure Table entities are created for activation.
- No profile field records a verification result.
- No public redemption behavior changes.
- No POS API, HMAC, barcode, profile lookup, or 1C behavior changes.
