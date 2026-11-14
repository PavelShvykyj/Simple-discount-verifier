# Worker: сборка и проверка

[История5](story-05-specification.md) определяет оставшуюся работу, [D2](message-contracts.md) — обмен, [inventory](environment-inventory.md) — целевые ресурсы. Перед кодом читать корневой и `agent-worker/AGENTS.md`; архитектуру — при изменении state/доверия.

## Локально

Из корня своего worktree, .NET SDK10.0.403 и PowerShell7:

```powershell
dotnet restore agent-worker/Worker.csproj --locked-mode
dotnet build agent-worker/Worker.csproj --no-restore
dotnet run --project tests/worker-client/WorkerClient.csproj -- --self-test
dotnet publish agent-worker/Worker.csproj -c Release -r linux-x64 --self-contained false --no-restore
dotnet restore tests/worker-hosted-smoke/HostedSmoke.csproj --locked-mode
dotnet build tests/worker-hosted-smoke/HostedSmoke.csproj --no-restore
pwsh -NoProfile -File tests/worker-hosted-smoke/Test-Local.ps1 -AssemblyPath tests/worker-hosted-smoke/bin/Debug/net10.0/HostedSmoke.dll
pwsh -NoProfile -File tests/worker-hosted-smoke/Test-Local.ps1 -AssemblyPath tests/worker-hosted-smoke/bin/Debug/net10.0/HostedSmoke.dll -StateReadGuards
```

Локальная матрица Worker ранее дала298 checks на подменах; её результат не Hosted PASS. Test-Local запускает временный loopback process/ключи и negative guards; StateReadGuards не вызывает настоящий Store и не доказывает isolation/CAS/TTL. Не запускать при занятых артефактах чужой задачи, не завершать процессы по историческим PID.

## Hosted ZIP

Публиковать framework-dependent linux-x64. ZIP — содержимое publish на верхнем уровне, не внешняя папка, ≤250MB; entry `dotnet Worker.dll`, `code_configuration`, runtime=`dotnet_10`, dependency_resolution=`bundled`, multipart. Проверить состав, отсутствие secrets/локальных settings, Linux native dependencies и ASP.NET shared framework. Записать SHA256 и соответствие active agent/version; source build без deployed SHA не доказывает проверенную версию. Действительные pins/protocol versions — в архитектуре.

HostedSmoke — отдельный диагностический проект/ZIP, не подмена Worker. StateReadProbe входит в тот же HostedSmoke.dll; отдельного MSBuild-флага HostedStateRead нет. Перед загрузкой диагностической версии проверить назначение, закрытые endpoints и минимальный scope; не включать основной бизнес-вход ради диагностики.

## Новое облачное окно

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

Подготовленная read-диагностика фиксирует project и probe identities `sdv:s5:probe:a/b`; перед вызовом сверить их с текущей реализацией/metadata. Она не готовый общий operational endpoint. При непроверяемом fault injection отметить NOT VERIFIED, не PASS. `turn.start/tool.result` текущего Hosted допуска не считать открытыми по одной успешной session.prepare. Done5 остаётся открытым до явной приёмки; историю6 не начинать.
