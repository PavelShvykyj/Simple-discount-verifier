# Агентское расширение

Начинать с [STATUS](STATUS.md). [DOCUMENTATION](DOCUMENTATION.md) определяет владельцев фактов, [GIT-WORKFLOW](GIT-WORKFLOW.md) — ветки и worktree. Требования и чтение по задаче — [SPEC](../_bmad-output/specs/spec-agent-extension/SPEC.md).

Код выпускается независимо: `agent-worker` — Hosted .NET/MAF; `agent-router` — Functions transport; `connector` — Windows/1С; `frontend` + `functions` — SWA. Перед изменением читать инструкции своего компонента.

Здесь находятся общие рабочие документы и [собственный IaC](infra/README.md). Корневой `infra/` основного приложения не импортируется.
