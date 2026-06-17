# Open Questions

1. SMS limits:
   - Need limits for repeated SMS requests and validation attempts.

2. Customer profile fields:
   - Decided for MVP in `docs/architecture/api-contract.md`.
   - One phone number represents one person and one profile.
   - Profile questionnaire fields are full name, birth date, and favorite dish.
   - Questionnaire answers are exchanged as `code`, `name`, `value`; answer
     type and questionnaire versions are not part of the MVP API/storage model.

3. Main application integration contract:
   - Decided for MVP in `docs/architecture/api-contract.md`.
   - POS validates web barcodes through `POST /api/pos/barcodes/validate`.
   - Server-to-server authentication uses HMAC with `x-client-id`,
     `x-timestamp`, and `x-signature`; nonce replay protection is deferred to a
     future phase.

4. Admin barcode support:
   - Decided for MVP in `docs/architecture/api-contract.md`.
   - Admins can inspect a redemption attempt by `correlationId`, including
     barcode status, profile data, audit events, and `ConsumedByScanId` when
     those data already exist.
   - QR-code access for the admin/support scenario is confirmed. The QR is
     generated from `correlationId` returned after the first public request, not
     from a manually typed code.
   - Manual support code entry is not included in the current scope.
   - The `scanner-survey` page must check QR/test-code reading from another app
     instance with the Angular `@zxing-js/ngx-scanner` integration.
   - Manager force approval is not implemented in the current scope and remains
     a future idea.
