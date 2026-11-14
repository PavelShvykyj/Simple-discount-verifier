# AGENTS

Для агентского расширения начинай с [STATUS](agent-extension/STATUS.md): приёмка, Build done и runtime evidence различаются. Читать только документ по задаче; владельцы фактов — [DOCUMENTATION](agent-extension/DOCUMENTATION.md), маршрут — [SPEC](_bmad-output/specs/spec-agent-extension/SPEC.md). Архив/история решений не обязательное чтение.

Следуй [Git/worktree](agent-extension/GIT-WORKFLOW.md). Перед изменением читай инструкции затронутого компонента: [Worker](agent-worker/AGENTS.md), [Router](agent-router/AGENTS.md), [Connector](connector/AGENTS.md), [tests](tests/AGENTS.md). Параллельные задачи — отдельные ветки/worktree.

Обмен меняется по [D2](_bmad-output/specs/spec-agent-extension/message-contracts.md); состояние/доступ/cleanup — по [архитектуре](_bmad-output/planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md). Проверь производителей/потребителей и совместимость установленных версий. SWA/Worker/Router/Connector выпускаются независимо; зависимостей между исполняемыми проектами и общего runtime-пакета контрактов нет. Choice — отдельный проект: его код/ресурсы/credentials не платформа этого расширения.

Не восстанавливай outbox/recovery/business replay. Сохраняй проверки trusted source/owner/branch/expiry/разрешённого действия. Гарантия1С и агентная оценка проверяются отдельно от API-тестов. Исходные чаты/фото/ответы/секреты не сохраняются в наших логах/БД/telemetry.

Существенные решения согласовывай и записывай в соответствующий memlog; статус/следующий шаг — в STATUS, evidence — в verification/done-review. Повторяемые процедуры оформляй специализированными skills в `.agents/skills/` без предварительной платформы. Сохраняй чужие изменения/evidence, не повторяй проверки без причины. Старые PID/права/очереди не live readback. Azure mutations/расходы, protected install/SYSTEM и новые live окна требуют отдельного конкретного разрешения; прежнее окно не переоткрывается по старому PASS. Архитектура не доказывает production-готовность.
