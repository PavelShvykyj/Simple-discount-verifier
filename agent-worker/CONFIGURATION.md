# Настройки Worker

Рабочий Worker не содержит fixtures, тестового signer или test mode. Настройки читаются при старте через `IOptions<WorkerOptions>`; после локальных изменений перезапустить Worker. Для изменения настроек Hosted создать новую версию агента. Ошибки `IValidateOptions` останавливают host до обслуживания запросов, сообщения содержат имена параметров, без значений.

## Источники

Публичный `appsettings.json` рядом с `Worker.dll` — полная схема с пустыми значениями. `Main` использует каталог приложения. Стандартные источники .NET: JSON → `appsettings.{Environment}.json` → environment variables → command line. Не передавать секреты через command line. Endpoint проекта задаётся только параметром `Worker:ProjectEndpoint`, включая штатное переопределение через `Worker__ProjectEndpoint`; специального источника или приоритета для него нет.

Локальный `appsettings.Development.json` хранится вне Git, с ACL владельца, исключён из publish; копирование в output разрешено только в Debug. Для его загрузки задаётся `DOTNET_ENVIRONMENT=Development`. Остальные локальные environment overlays также исключены из Git/publish. В Hosted значения задаёт оператор через настройки окружения, а не код.

## Полная схема

Все поля обязательны; для каждого словаря требуется хотя бы одна запись. Секретов в этой схеме нет: Worker использует managed identity и только публичные RSA-ключи. Endpoints, имена инфраструктуры и публичные ключи не логируются.

| Путь | Назначение / формат | Пример формы environment variable |
|---|---|---|
| `Worker:Agent` | Ожидаемое имя агента | `Worker__Agent` |
| `Worker:Version` | Ожидаемая версия, сверяется с платформой | `Worker__Version` |
| `Worker:ProjectEndpoint` | HTTPS endpoint проекта, без credentials/query/fragment | `Worker__ProjectEndpoint` |
| `Worker:ServiceBusNamespace` | Полное DNS-имя, без scheme/path | `Worker__ServiceBusNamespace` |
| `Worker:PubSubEndpoint` | HTTPS endpoint WPS, без credentials/query/fragment | `Worker__PubSubEndpoint` |
| `Worker:PubSubHub` | Имя hub: буква, затем буквы/цифры/underscore, до 128 символов | `Worker__PubSubHub` |
| `Worker:Queues` | Словарь `branchId → queue`; branch — 1–128 цифр; queue — 1–260 букв/цифр/`._-`; очереди разных филиалов различаются без учёта регистра | `Worker__Queues__<branchId>` |
| `Worker:SwaPublicKeys` | Словарь `kid → PEM PUBLIC KEY` или `RSA PUBLIC KEY`, RSA ≥2048 | `Worker__SwaPublicKeys__<kid>` |
| `Worker:RouterPublicKeys` | Такой же словарь, отдельные от SWA ключи | `Worker__RouterPublicKeys__<kid>` |

В PEM передаются реальные переводы строк. Private PEM отвергаются. Для ротации сначала доставить новый public kid, затем переключить signer; старый public key держать до max expiry+skew. Не использовать `:`/`__` в kid конфигурации: это разделители configuration provider. Issuer/audience заданы протоколом [D2](../_bmad-output/specs/spec-agent-extension/message-contracts.md), а не переключателем окружения.

`FOUNDRY_HOSTING_ENVIRONMENT`, `FOUNDRY_AGENT_NAME`, `FOUNDRY_AGENT_VERSION`, `FOUNDRY_AGENT_SESSION_ID` принадлежат платформе. Worker получает их через SDK Foundry. В Hosted имя и версия сверяются с настройками при старте. Ошибка останавливает запуск. Контекст пользователя, Call ID и селектор сессии проверяются на запросе.

## Переход на новую схему

| Прежнее имя | Новое имя |
|---|---|
| `WORKER_AGENT`, `WORKER_VERSION` | `Worker__Agent`, `Worker__Version` |
| `WORKER_SERVICE_BUS_NAMESPACE` | `Worker__ServiceBusNamespace` |
| `WORKER_PUBSUB_ENDPOINT`, `WORKER_PUBSUB_HUB` | `Worker__PubSubEndpoint`, `Worker__PubSubHub` |
| `WORKER_QUEUE_<branchId>` | `Worker__Queues__<branchId>` |

Задать также `Worker__ProjectEndpoint` и публичные ключи SWA и Router согласно таблице. Прежние `WORKER_*` не читаются. Настройки профиля, тестового подписанта и backend private key удалены без замены.

## Логирование

Уровни задаются через `Logging:LogLevel`, формат консоли — через `Logging:Console`. Каталог технических событий — [WorkerLog.cs](WorkerLog.cs). AzureTransport различает явный отказ, отмену и неподтверждённый исход. `CommandAccepted` и `NotificationAccepted` подтверждают приём вызова соответствующим сервисом, `TurnFinalized` — запись terminal CAS.

В прикладных логах и scopes запрещены исходные данные запросов и ответов, route, owner/session, номер заказа и секреты. Для недоверенного входа пишутся коды причин, для инфраструктурных ошибок сохраняется исключение. Настройка экспорта — [Hosted monitoring](../agent-extension/infra/hosted-monitoring.md), запросы и корреляция — [Worker runbook](../_bmad-output/specs/spec-agent-extension/worker-runbook.md#просмотр-логов-в-application-insights).

[Microsoft Options](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) · [Microsoft Configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0) · [Microsoft Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/)
