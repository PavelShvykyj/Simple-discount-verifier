# Windows Connector

Начинай с [STATUS](../agent-extension/STATUS.md). [D2](../_bmad-output/specs/spec-agent-extension/message-contracts.md) — при изменении обмена; [runbook](../_bmad-output/specs/spec-agent-extension/connector-runbook.md) — сборка/управление; тесты — `tests/connector-tests`.

Один экземпляр — один branch/база1С, API loopback. Без зависимостей на исполняемые SWA/Worker/Router, Foundry/AI и код/ресурсы Choice.

- Последовательный ReceiveAndDelete через ServiceBusProcessor/MaxConcurrentCalls=1. Branch/формат/expiry доAPI; identity заказа=branch+номер, сумма hint. Непрозрачные route/корреляция возвращаются неизменёнными.
- Basic на каждый запрос; GET PingPath с IBSession:start → cookie ibsession, finish закрывает. Перед lookup ping/recreate; неудачная подготовка запрещает lookup. Все API1С должны быть идемпотентны/безопасны при повторе; фактические пути/форматы и гарантия проверяются отдельно.
- Machine outcome/reasonCode, HTTP200 не бизнес-успех. Неоднозначный API/Send не повторять автоматически; без journal/commandId registry/replay. Переданная команда может исполниться до expiry, немедленного отзыва нет. Lookup реализован; FiscalizeReceipt отклоняется без API.
- Generic Host/Options/IValidateOptions; Host читает каталог exe, настройки меняются restart. Публичный appsettings.json — полная схема с пустыми credentials; локальный Development overlay под ACL, вне Git/publish, Debug copy допустим. Secrets не попадают в artifact/logs.
- Queue-scoped Listen own commands/Send results. Telegram allowlist/freshness и общий gate с lookup, manual stoplistener; без SCM/process restart/business retry. Enabled flags — конфигурация окружения.

Ручные install/update; SYSTEM из repo не запускать. Protected helpers/live/SB/Telegram/права/расходы требуют отдельного scope; PID только после ownership/startTime/readback. У приложения нет self-test/test modes. Перед выпуском проверить branch/expiry/envelope/lost result, AlreadyFiscalized, повторы/parallel/ambiguity и реальную гарантию1С.

Перед live проверять совместимость установленного Connector с Worker по версиям [D2](../_bmad-output/specs/spec-agent-extension/message-contracts.md).
