---
name: deploy-azure-tenant
description: Deploy and verify a new Simple Discount Verifier tenant in its own Azure account or subscription, including the release branch, tenant files, Azure resources, Static Web Apps settings, GitHub deployment token, cleanup Logic App, and smoke tests. Use when onboarding a tenant, recreating its Azure infrastructure, or resuming an interrupted tenant deployment. Always use the personal azure-cli-local skill for Azure access and require approval before every step, including fully automatic and read-only steps.
---

# Deploy Azure Tenant

Use `docs/process/deploy-azure-tenant.md` as the canonical procedure. Read it completely before starting or resuming a deployment.

## Required companion skill

Load and follow `azure-cli-local` before any Azure CLI action. If it is unavailable, do not execute Azure commands; use the runbook only as a human handoff.

## Step protocol

For every runbook step, including local checks and read-only commands:

1. Show the step number and objective.
2. Separate `Automatic` actions from `User` actions.
3. Show every exact command with resolved non-secret identifiers.
4. State the target, expected effect, expected result, and whether state changes.
5. Ask `Разрешаете выполнить шаг N?` and stop.
6. Execute only that step after explicit approval.
7. Report actual output without secrets, then present the next step and stop again.

Never treat approval for one step as approval for another. Keep each Azure state-changing command in its own step. One approved Bicep deployment invocation may create or update all resources disclosed by its reviewed `what-if`.

## Safety and state

- Start or resume from the runbook checkpoint; never restart blindly.
- Request tenant ID, subscription ID, tenant slug, release branch, and region. Never reuse identifiers from another tenant.
- Require a lowercase Latin tenant slug. Derive the normal suffix as `-<slug>`; omit the hyphen only where Azure Storage naming rules prohibit it.
- Create `release/<slug>` from current `master`. Do not route tenant onboarding through `develop` or a PR to `master`.
- Run `infra/scripts/New-TenantDeploymentFiles.ps1` instead of hand-writing tenant parameters and workflow YAML.
- Revalidate provider registration, name availability, region eligibility, pricing/SKU, and current Azure inventory live.
- Do not put secrets, deployment tokens, connection strings, Azure auth files, or real local parameter values in chat or Git.
- Before Azure network calls, pause for the user to disable Avast Web/HTTPS Shield as required by `azure-cli-local`. After the final Azure call, pause until the user confirms shields are enabled again.
- If output differs from the runbook expectation, stop and diagnose the current step. Do not skip forward.

## Completion

Finish only when the final inventory, GitHub Actions deployment, public/API smoke tests, cleanup scheduler run, administrator access, and Avast restoration are confirmed. Summarize created resources, branch, hostname, GitHub secret name, optional items left disabled, and the last successful checkpoint without exposing secret values.
