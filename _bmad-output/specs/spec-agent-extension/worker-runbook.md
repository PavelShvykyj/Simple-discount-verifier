# Worker: сборка и проверка

[История5](story-05-specification.md) определяет оставшуюся работу, [D2](message-contracts.md) — обмен, [inventory](environment-inventory.md) — целевые ресурсы. Перед кодом читать корневой и `agent-worker/AGENTS.md`; архитектуру — при изменении state/доверия.

## Локально

Из корня репозитория, с .NET 10 SDK и PowerShell 7:

```powershell
dotnet restore agent-worker/Worker.csproj --locked-mode
dotnet build agent-worker/Worker.csproj --no-restore
dotnet test tests/worker-client/WorkerClient.csproj
dotnet test tests/connector-tests/Connector.Tests.csproj
dotnet publish agent-worker/Worker.csproj -c Release -r linux-x64 --self-contained false --no-restore
dotnet restore tests/worker-hosted-smoke/HostedSmoke.csproj --locked-mode
dotnet build tests/worker-hosted-smoke/HostedSmoke.csproj --no-restore
pwsh -NoProfile -File tests/worker-hosted-smoke/Test-Local.ps1 -AssemblyPath tests/worker-hosted-smoke/bin/Debug/net10.0/HostedSmoke.dll
pwsh -NoProfile -File tests/worker-hosted-smoke/Test-Local.ps1 -AssemblyPath tests/worker-hosted-smoke/bin/Debug/net10.0/HostedSmoke.dll -StateReadGuards
```

Worker и Connector проверяются отдельными xUnit-проектами. `Test-Local.ps1` запускает отдельный loopback-процесс. `-StateReadGuards` проверяет отказы на подменах. Реальные isolation/CAS/TTL проверяются в Hosted. [Настройки Worker](../../../agent-worker/CONFIGURATION.md).

## Просмотр логов в Application Insights

Сначала выполнить [подключение Hosted monitoring](../../../agent-extension/infra/hosted-monitoring.md). В Application Insights → Logs:

```kusto
traces
| order by timestamp desc
| take 100
```

В Log Analytics workspace:

```kusto
AppTraces
| order by TimeGenerated desc
| take 100
```

Для поиска неподтверждённых эффектов использовать `traces | where message has "no confirmed completion"`. Сопоставлять `MessageEventId`, `RequestId`, `OperationId` и `CommandId` с ключами корреляции SDK. Числовой EventId логгера — идентификатор типа записи. Для подробной диагностики CAS включить Debug только для `Sdv.Worker.FoundryState` и `Sdv.Worker.Lookup`.

## Просмотр технических логов Hosted

Для существующей согласованной сессии:

```powershell
azd ai agent monitor <agent-name> --session-id <session-id> --follow
```

Для делегированной сессии требуется тот же `--user-identity`. Предварительные требования и REST-вариант `:logstream` — в [документации Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/monitor-hosted-agent-logs). В stdout/stderr доступна также диагностика запуска до регистрации провайдеров логов.

## Состав Hosted ZIP

Публиковать framework-dependent linux-x64. ZIP — содержимое publish на верхнем уровне, не внешняя папка, ≤250MB; entry `dotnet Worker.dll`, `code_configuration`, runtime=`dotnet_10`, dependency_resolution=`bundled`, multipart. Проверить состав, отсутствие secrets/локальных settings, Linux native dependencies и ASP.NET shared framework. Записать SHA256 и соответствие active agent/version; source build без deployed SHA не доказывает проверенную версию. Действительные pins/protocol versions — в архитектуре.

HostedSmoke — отдельный диагностический проект/ZIP, не подмена Worker. StateReadProbe входит в тот же HostedSmoke.dll; отдельного MSBuild-флага HostedStateRead нет. Перед загрузкой диагностической версии проверить назначение, закрытые endpoints и минимальный scope; не включать основной бизнес-вход ради диагностики.

## Новое облачное окно

До окна получить актуальный `instance_identity.principal_id` целевого агента и сверить grants именно Agent Identity по [инфраструктурной процедуре](../../../agent-extension/infra/README.md#identity-hosted-worker). Project/Router MI и их права не являются правами Worker; token-service endpoint/header предоставляет Foundry, в appsettings их не копировать.

Прежние live окна закрыты. Перед Azure читать azure-cli-local, предъявить конкретные version/hash, identities, команды/длительность/stop condition, разрешённые эффекты и стоимость; mutations/расходы/новый сертификат/real transport требуют соответствующего разрешения. Старые metadata/certificates не reusable authorization. Сначала проверить пригодность имеющихся артефактов/signer, использовать существующий agent и роли; не пересоздавать их автоматически.

| Этап | Условие зачёта |
|---|---|
| Preflight | Нужный project/agent/version/SHA; текущие signer/public verifier/metadata/срок, fixed routing, действительные scopes и выбранный безопасный профиль API |
| Session prepare | Только подписанный bounded server-only prepare выбранных A/B; invalid signature/owner/replay отклоняются. Модели/фискальные tools закрыты |
| State read | Каждый A/B видит собственную IdleEmpty с конкретным ETag и не видит чужую; оба403/обаmissing — не isolation PASS |
| CAS/TTL/idle | Реальный specific-ETag conflict, no missing upsert, retention/expiry и MAF hydration проверяются отдельно; ETag в read их не доказывает |
| Start/result | Только после доказанного source/owner/queue/state admission; чужой/late/duplicate/start-as-result не запускает continuation |
| E2E | Safe LookupOrder → собственный command queue → Connector/API → results → Hosted → собственный PubSub; два пользователя/филиала, faults/ACK/deadline и фактические SDK attempts |
| Telemetry/stop | Нет content/identity/secrets в spans/errors/Logic App history; окно остановлено, ресурсы/Store не удалять без отдельной команды |

Подготовленная read-диагностика фиксирует project и probe identities `sdv:s5:probe:a/b`; перед вызовом сверить их с текущей реализацией/metadata. Она не готовый общий operational endpoint. При непроверяемом fault injection отметить NOT VERIFIED, не PASS. `turn.start/tool.result` текущего Hosted допуска не считать открытыми по одной успешной session.prepare. Условия допуска проверяются отдельно. Совместная приёмка историй 5 и 6 — по [плану](stories.yaml).
