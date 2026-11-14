# Статус агентского расширения

Граница подтверждённого; не live readback. Приёмка истории, Build done, локальные тесты и runtime evidence — разные факты.

| Область | Подтверждено | Открыто |
|---|---|---|
| План | Истории1–4 приняты; спецификация5 принята | Done5 открыт, историю6 не начинать до явной приёмки5 |
| Worker | Локальная основа,298 checks на подменах; Hosted signed session.prepare A/B; invalid signature/owner/replay отклоняются | Cloud turn.start/tool.result закрыты; реальные isolation/CAS/TTL/MAF idle/E2E/PubSub/telemetry не доказаны |
| Read-диагностика | Локально подготовлена | Не загружена; metadata/certificate истекли, пригодность ZIP/signer проверяется перед новым окном |
| Connector | Options/IHttpClientFactory/ServiceBusProcessor, Basic+IBSession;99 xUnit tests; dev NotFound/OrderMissing и Telegram подтверждены | Real Found, новая SCM-установка/миграция/Update/rollback и helpers с текущей конфигурацией не проверены |

Следующий шаг: для [истории5](../_bmad-output/specs/spec-agent-extension/story-05-specification.md) проверить имеющиеся diagnostic ZIP/metadata/signer и подготовить конкретное новое облачное окно по [Worker runbook](../_bmad-output/specs/spec-agent-extension/worker-runbook.md). Не пересоздавать существующие ресурсы/роли/apps и не повторять успешные проверки без причины. Не менять Worker ради диагностики без объяснения и согласования объёма.

Сначала read-isolation A/B, затем отдельные CAS/TTL/idle, source admission и E2E. Не удалять Store/тестовые ресурсы и не закрывать5 автоматически. Все прежние live окна закрыты; новые Azure mutations/расходы, protected install/SYSTEM и live обмен требуют конкретного разрешения. Старые PID/права/queue counts не устанавливают текущего состояния.
