# Open Questions

1. Final web barcode format:
   - Code 128 is preferred.
   - Must be tested with real scanners and the main restaurant application.

2. Exact web-code value format:
   - Prefix or pattern must reliably distinguish it from EAN13 discount cards.

3. One active barcode policy:
   - Reject a new request while an active code exists, or invalidate the previous active code and issue a new one.

4. SMS provider and SMS limits:
   - Exact provider is not decided.
   - Need limits for repeated SMS requests and validation attempts.

5. Administrator authorization:
   - Exact authorization mechanism is not decided.

6. Customer profile fields:
   - Final field list is not decided.

7. Main application integration contract:
   - Exact validation API request and response format must be agreed.
   - Returned lookup key is expected to be phone number or another agreed key.
