# IaC агентского расширения

Читать только для инфраструктурной задачи. [STATUS](../STATUS.md) — граница проверенного, [inventory](../../_bmad-output/specs/spec-agent-extension/environment-inventory.md) — целевые адреса, [архитектура](../../_bmad-output/planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md) — runtime/trust. Код [main.bicep](main.bicep) — источник точных параметров/ресурсов.

Subscription wrapper создаёт отдельную RG и локальный resources module. Корневой `infra/`, existing SWA и приложения не импортируются. Worker ZIP bundled dotnet_10 без ACR; поставка — [Worker runbook](../../_bmad-output/specs/spec-agent-extension/worker-runbook.md).

| В шаблонах | Границы |
|---|---|
| Foundry S0 account/project с MI, local auth disabled | Без model deployments/Hosted runtime grants |
| SB Basic: input/results и2commands; TTL120s,lock60s,MaxDelivery3 | Без Sessions/dedup/partitioning; entity Listen own commands/отдельный Send results, без secret outputs/listKeys |
| Router Linux Flex FC1 isolated10,MI,2048MiB,max1,no always-ready | Private deployment blob/StorageV2 LRS без shared keys; Storage служебный, не БД операций |
| Router4scoped roles | Receiver input/results, Blob Data Owner своего Storage, Metrics Publisher своего Insights; principalId входит в GUID assignments |
| Workspace/Insights | Retention30дней,caps1GB/day,AAD ingestion,content flags false; SDK whitelist проверяется отдельно |
| Web PubSub | Примеры Free_F1/1,local auth disabled; negotiation/publish grants не созданы |

SWA credential/input Sender/upload/delegation, Hosted Store/files/commands Sender/publisher, Router invoke/UserIdentityImpersonation, отдельные PS256 signing keys, Windows ACL/credentials и maintenance identity/Logic App — отдельные runtime задачи. Не выдавать placeholder broad roles, не добавлять Queue/Table host roles заранее. IAM/resources metadata не доказывают SDK access, two-user isolation, 1С или E2E.

## Локальная проверка

PowerShell7, уже установленный Bicep CLI; без Azure/profile/network/AVM restore/установок:

```powershell
./agent-extension/infra/scripts/Test-Infrastructure.ps1 -BicepPath '<installed-bicep.exe>'
# Для точного согласованного nonsecret файла:
./agent-extension/infra/scripts/Test-Infrastructure.ps1 -BicepPath '<installed-bicep.exe>' -ParameterFile '<absolute-parameters.json>'
```

Без ParameterFile проверяются оба [develop](parameters/develop.example.json)/[production](parameters/production.example.json) примера: schema/types/limits, resource types, очереди/policies, runtime/MI/RBAC scopes и secret/telemetry boundaries. Это compile/static assertions, не ARM expression emulator/capacity/runtime PASS. Script использует случайный каталог внутри `agent-extension/.local`, проверяет путь перед удалением только своих временных файлов; bundle cache остаётся, DOTNET_BUNDLE_EXTRACT_BASE_DIR восстанавливается.

BCP081 для CurrentBillingFeatures@2015-05-01 означает отсутствие локальной type schema; warning не скрывать, фактический cap проверять readback. Примеры не inventory и не разрешение production. Реальные имена/region/branches/SKU задаются отдельным nonsecret parameter file. Не переключать Free_F1 на платный SKU без согласования; cap/max instances не денежный предел.

## Применение

Перед Azure читать azure-cli-local; существующая CLI session, явно согласованная subscription, права RG/resources/scoped role assignments. Проверить доступность Hosted/State Store/text/vision, quotas/providers и unique names; уже выполненные регистрации не повторять без причины. Отсутствие нужной возможности блокирует применение до согласованного решения.

Порядок: local PASS точного файла → subscription what-if → просмотр каждого diff/стоимости → отдельное разрешение apply → metadata readback → повторный what-if. Subscription wrapper сам создаёт RG. Каждая mutation/расходы/production требуют конкретного разрешения; drift не применять автоматически.

```powershell
$subscriptionId = '<approved-subscription>'
$deploymentLocation = '<approved-location>'
$parameterFile = '<absolute-reviewed-nonsecret-parameters.json>'
$deploymentName = '<unique-reviewed-name>'
az deployment sub what-if --subscription $subscriptionId --location $deploymentLocation --name $deploymentName --template-file ./agent-extension/infra/main.bicep --parameters "@$parameterFile"
# Только после отдельного разрешения:
az deployment sub create --subscription $subscriptionId --location $deploymentLocation --name $deploymentName --template-file ./agent-extension/infra/main.bicep --parameters "@$parameterFile" --query properties.provisioningState -o tsv
```

Readback: RG/location, ресурсы/SKU/MI/runtime/storage flags,4queues/4entity policy names+rights без keys, retention/caps, WPS и4Router roleDefinition/principal/scope. Не читать полные appsettings/publish profiles/credentials. Сохранить безопасные version/input hashes/date/наблюдения; повторить what-if с теми же subscription/template/parameters и разобрать defaults/normalization/unsupported, не объявлять zero-drift по Ignore. Созданные ресурсы не доказывают production-готовность. Полная цена/квоты/фактическая доступность требуют проверки перед применением.
