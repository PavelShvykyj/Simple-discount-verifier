# Architecture Decisions

## Accepted for initial version

- Frontend: Angular + Ionic.
- Backend: Azure Functions.
- Storage: Azure Table Storage.
- Secrets: Azure Key Vault.
- Hosting: Azure.
- SMS: external SMS provider likely, SMS-Fly is a candidate.
- Main restaurant application does not expose public API.
- Main restaurant application can act as a REST client.
- Discount calculation is owned by the main restaurant application.
- Web service does not calculate discount amount.
- One-time barcode is invalidated immediately after successful validation.
- Customer profile has no complex workflow state.
- SMS verification during profile creation is out of scope for initial release.