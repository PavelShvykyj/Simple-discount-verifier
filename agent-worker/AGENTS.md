# Agent Worker

Начинай с [STATUS](../agent-extension/STATUS.md) и [спецификации5](../_bmad-output/specs/spec-agent-extension/story-05-specification.md). [Runbook](../_bmad-output/specs/spec-agent-extension/worker-runbook.md) читать для сборки/проверки; [D2](../_bmad-output/specs/spec-agent-extension/message-contracts.md) — при изменении обмена, [архитектуру](../_bmad-output/planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md) — состояния/доступа/cleanup.

Worker владеет guard/MAF/tools/state/финалом: custom Invocations, ZIP bundled `dotnet_10`. Без queue consumer в sandbox, зависимостей на исполняемые SWA/Router/Connector и заранее созданной provider abstraction. Managed definitions — только согласованное исключение.

- Делегированный user не доказывает источник tool.result. Selector query `agent_session_id` не MAF ID. Runtime проверяет tools/аргументы; fiscalization требует явного разрешения администратора.
- Create-only и конкретный ETag/CAS до effects; control+snapshotRef атомарны, полное waiting до Send. После Send старый handler не пишет state/history. Второй writer, replay/auto recovery запрещены.
- Бизнес-успех определяет1С. Неоднозначный Send/API не разрешает retry; финал через Web PubSub в исходном deadline.
- Deadline, TTL и удаление upload независимы. Telemetry без содержимого чатов/фото/секретов, включая auto-capture.

Локальные подмены не доказывают Hosted isolation/CAS/TTL/MAF/E2E. Новые cloud окна/права/расходы согласуются отдельно; старые разрешения не переиспользуются. Done5 открыт; историю6 не начинать.
