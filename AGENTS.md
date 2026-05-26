# AGENTS

Read first:

- `docs/product/discount-verification-summary.md`
- `docs/product/speckit-specify.md`
- `docs/product/open-questions.md`
- `docs/architecture/architecture-decisions.md`
- `docs/architecture/data-lifecycle.md`

Rules:

- The service verifies discount eligibility; it never calculates discounts.
- Customer profiles are created by an administrator before redemption.
- SMS verification is required only during discount redemption in the initial release.
- Issue a barcode only after successful SMS verification.
- One-time barcode codes are short-lived, single-use, and invalidated after successful validation.
- Do not log raw phone numbers or reusable secrets; use `correlationId` and `phoneHash`.
- Do not add profile workflow states, discount calculation, or sale cancellation handling unless requirements change.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
