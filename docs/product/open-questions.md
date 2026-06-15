# Open Questions

1. SMS limits:
   - Need limits for repeated SMS requests and validation attempts.

2. Customer profile fields:
   - Final field list is not decided.

3. Main application integration contract:
   - Exact validation API request and response format must be agreed.
   - Server-to-server authentication is decided for this phase: POS-facing APIs
     use HMAC with `x-client-id`, `x-timestamp`, and `x-signature`; nonce replay
     protection is deferred to a future phase.
