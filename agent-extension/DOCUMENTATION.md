# Владельцы фактов

После STATUS открывать только документ для текущей задачи. Маршруты — в [SPEC](../_bmad-output/specs/spec-agent-extension/SPEC.md); companions автоматически не загружать.

| Факт | Владелец |
|---|---|
| Приёмка, граница проверенного, следующий шаг | [STATUS](STATUS.md) |
| Инварианты, state, trust, cleanup | [Архитектура](../_bmad-output/planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md) |
| CAP / точные wire/API поля | [SPEC](../_bmad-output/specs/spec-agent-extension/SPEC.md) / [D2](../_bmad-output/specs/spec-agent-extension/message-contracts.md) |
| AC / порядок и обязательные checkpoints | [Критерии](../_bmad-output/specs/spec-agent-extension/acceptance-criteria.md) / [план](../_bmad-output/specs/spec-agent-extension/stories.yaml) |
| Открытая история | [Спецификация5](../_bmad-output/specs/spec-agent-extension/story-05-specification.md) |
| Исполнение | [Worker](../_bmad-output/specs/spec-agent-extension/worker-runbook.md), [Connector](../_bmad-output/specs/spec-agent-extension/connector-runbook.md), [IaC](infra/README.md) |
| Конфигурация Worker | [Схема, источники и переход на новые настройки](../agent-worker/CONFIGURATION.md) |
| Подготовка Hosted monitoring | [Связь Foundry → Insights, Entra grants и preflight](infra/hosted-monitoring.md) — обязательный шаг после IaC |
| Runtime identity Hosted Worker | [Автоматическое назначение identity и подготовка RBAC](infra/README.md#identity-hosted-worker) |
| Ресурсы develop | [Inventory](../_bmad-output/specs/spec-agent-extension/environment-inventory.md); параметры IaC — примеры |
| Ветки/worktree | [GIT-WORKFLOW](GIT-WORKFLOW.md) |

Правило пишется у владельца; остальные документы ссылаются на него. AGENTS содержит обязательные инструкции, не хронику/копию спецификации. ID/числа/negative cases и границы доказанного сохраняются; меняя пути, исправлять входящие ссылки и frontmatter.

STATUS обновлять по фактическому evidence и явной приёмке. Новые результаты фиксировать в verification/done-review текущей задачи с версией, условиями, ожиданием/наблюдением и ограничениями, без содержимого/секретов. Существенные новые решения согласовывать и записывать в соответствующий memlog; он не обязательное чтение. Архивные review/handoff/старые разрешения не являются зависимостями рабочей документации.
