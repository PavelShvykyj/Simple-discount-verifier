# Agent Router

Начинай с [STATUS](../agent-extension/STATUS.md). При изменении обмена читай [D2](../_bmad-output/specs/spec-agent-extension/message-contracts.md), состояния/доступа — [архитектуру](../_bmad-output/planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md).

Отдельный транспортный Function App: Service Bus Basic → фиксированный Hosted endpoint. Без моделей/1С/прикладной БД/зависимостей на исполняемые проекты других компонентов; Host Storage не хранит диалоги. Выпуск независим от SWA.

- PeekLock input/results; commands Connector — ReceiveAndDelete. Тип задаёт доверенная очередь, не payload. До invoke проверить D2/подпись/expiry/branch; endpoint/agentName только из server config.
- Selector — query `agent_session_id`; свежий platform call ID не передаётся Connector. `x-ms-user-identity` требует UserIdentityImpersonation и проверки owner приложением.
- Complete только после полного HTTP200 JSON ACK исходного eventId: Processed/Duplicate/Discarded. Detached Task/202/SSE не ACK. CAS выполняет Worker; redelivery не разрешает business replay/recovery. `maxConcurrentCalls=1` не сериализует instances; bounded retries/settlement/DLQ проверяются при реализации.
- Передавать текст/структурированный результат/ссылку upload, без binary фото. Telemetry только whitelist без content/secrets/routing context.

Проверять двух users, branch/signature/version substitution, expiry/idle, duplicates/races/multiple instances, lost ACK/lock loss, Foundry failure/deadline. Исполняемого проекта нет; команды не выдумывать. Создание проекта, добавление dependencies/workflows и работы в Azure — по отдельному поручению. Историю 6 можно начать до Done5. Порядок совместной приёмки — в [плане](../_bmad-output/specs/spec-agent-extension/stories.yaml).
