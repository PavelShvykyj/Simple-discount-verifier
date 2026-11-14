# Connector: разработка и эксплуатация

Читайте этот файл для сборки/управления; [D2](message-contracts.md) — при изменении обмена, [архитектуру](../../planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md) — при изменении поведения/доверия. Перед кодом обязательны корневой и `connector/AGENTS.md`.

## Локальная разработка

Из корня своего worktree, .NET10 и PowerShell7:

```powershell
dotnet test tests/connector-tests/Connector.Tests.csproj
pwsh -NoProfile -File connector/Manage-Connector.ps1 -Action SelfTest
```

SelfTest проверяет helper, не делает installed-service PASS. У приложения нет `--self-test`/`--check-config`. Подтверждены99 xUnit tests и read-only NotFound/OrderMissing на dev; это граница evidence, не результат нового запуска. Found на реальной1С, новая SCM-установка/миграция/rollback не подтверждены.

Generic Host/Options/IConfiguration, startup validation, IHttpClientFactory и ServiceBusProcessor. `connector/appsettings.json` — публичная схема с placeholders; `connector/appsettings.Development.json` — локальные значения под ACL, вне Git/publish (Debug copy допустим). Никогда не публиковать заполненные secrets. Настройки действуют после restart; credentials из защищённого локального файла не копируются в feature Git.

```powershell
pwsh -NoProfile -File connector/Publish-Connector.ps1 -OutputDirectory agent-extension/.local/connector-publish
```

OutputDirectory должен быть пустым и внутри `agent-extension/.local`. Публикация win-x64 framework-dependent, без trim/single-file; проверяются public settings/отсутствие Development, создаётся artifact-manifest v2 с SHA256.

## Установка и выпуск

`Manage-Connector.ps1`: Action=`SelfTest|Prepare|Install|Validate|Start|Stop|Update|Remove`; параметры `PublishDirectory,ExpectedArtifactHash,ReleaseId,SettingsPath,DeploymentEnvironment` (develop). Branch берётся из выбранного settings, отдельного параметра Branch нет. SettingsPath — локальный файл, не network/reparse path. Сверить SHA256/manifest, конфигурацию/ACL, версию .NET и службу до применения.

Prepare/Install/Start/Stop/Update/Remove и SYSTEM/live обмен требуют отдельного конкретного разрешения. До него только подготовка артефакта. Helpers с новой конфигурацией ещё не подтверждают lifecycle/rollback: проверить partial update, фактическую конфигурацию/версию из SCM, учётную запись и доступ, Start/Stop, следующий отдельный lookup и возврат версии. Не повторять business command при неопределённом исходе. Не заявлять successful install по exit code publish/SelfTest.

## Runtime и управление

Одна установленная служба — одна база/branch; ReceiveAndDelete, последовательная команда, отдельная собственная Basic+IBSession API session. Queue Listen только своего филиала, results Send без namespace Manage. Deadline и результат по D2; application API/Send retries=0. Сервис не ведёт persistent command registry/recovery queue. Нет background API ping; проверка/подготовка session только на команду или явно разрешённый management check.

| Telegram | Правило |
|---|---|
| Допуск | Только private chat, точное совпадение chat+sender allowlist; не bot/group/forward/edit/unknown command |
| Freshness/replay | Возраст≤60s, future skew≤5s; backlog при старте отбрасывается. Последовательный in-memory offset/high-watermark, без восстановления действий из файлов |
| Управление | status/check, stop/start/reset API, stop/start/recreate listener; без shell/SCM/process restart |
| Сериализация | Общий gate с lookup; Busy сразу, никаких deferred actions. Management timeout≤5s, без повтора business request |
| Intent | Reset API не запускает вручную остановленный listener; startup возвращает начальный enabled intent |
| Ответы | Короткие состояния; cached status с freshness, не обещать live API по старому cache. Полные URLs/tokens не выводятся |

Начальные flags Telegram/listener задаются окружением и согласуются; конкретный тестовый branch не архитектурная константа. Нужные команды/IDs извлекать из текущей конфигурации, не из старого окна.

Диагностика: безопасные stage/outcome/reason/duration/correlation, daily Serilog последние10файлов. Без текстов/фото/raw API response/user identity/payload/route/Authorization/Basic/cookie/SAS/connect URLs. Лог — не audit/execution journal. Неизвестное состояние/потеря ответа не превращаются в success или право повторной фискализации.
