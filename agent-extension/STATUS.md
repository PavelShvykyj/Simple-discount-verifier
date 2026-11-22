# Статус агентского расширения

Граница подтверждённого; не live readback. Приёмка истории, Build done, локальные тесты и runtime evidence — разные факты.

| Область | Подтверждено | Открыто |
|---|---|---|
| План | Истории 1–4 и спецификация 5 приняты | Историю 6 можно начать. В рамках неё можно дополнить и завершить работы истории 5. Done5 и Done6 остаются открыты до совместной приёмки по [плану](../_bmad-output/specs/spec-agent-extension/stories.yaml) |
| Worker | Ревью текущего кода завершено. Для прежней версии зафиксированы 298 проверок на подменах и Hosted signed Prepare A/B с отрицательными проверками signature/owner/replay | Текущая версия Worker и изменения Connector v2 ещё не проверены сборкой и xUnit. Hosted Start/Result закрыты до совместимого Connector v2, Router с resultProof и проверки доступа. Изоляция, CAS/TTL, MAF hydration, E2E, PubSub и доставка telemetry не подтверждены |
| Read-диагностика | Локально подготовлена | Не загружена; metadata/certificate истекли, пригодность ZIP/signer проверяется перед новым окном |
| Connector | Options/IHttpClientFactory/ServiceBusProcessor, Basic+IBSession;99 xUnit tests; dev NotFound/OrderMissing и Telegram подтверждены | Real Found, новая SCM-установка/миграция/Update/rollback и helpers с текущей конфигурацией не проверены |

Следующие шаги: локальная проверка Worker и Connector v2 по [Worker runbook](../_bmad-output/specs/spec-agent-extension/worker-runbook.md) и работа над Router по истории 6. Перед новым облачным окном проверить имеющиеся diagnostic ZIP/metadata/signer и условия допуска. Не пересоздавать существующие ресурсы/роли/apps и не повторять успешные проверки без причины. Не менять Worker ради диагностики без объяснения и согласования объёма.

Сначала read-isolation A/B, затем отдельные CAS/TTL/idle, source admission и E2E. Не удалять Store/тестовые ресурсы и не закрывать истории 5 и 6 по частичному результату. Все прежние live окна закрыты; новые Azure mutations/расходы, protected install/SYSTEM и live обмен требуют конкретного разрешения. Старые PID/права/queue counts не устанавливают текущего состояния.
