# Целевое окружение develop

Справочник адресации, не live inventory и не разрешение действий. Перед облачным действием сверить существующие ресурсы, active agent/version, identities/scopes, очереди/службы/keys и согласовать конкретное окно. Ресурсы/роли/приложения автоматически не пересоздавать. См. [Worker runbook](worker-runbook.md), [архитектуру](../../planning-artifacts/architecture/architecture-agent-extension/ARCHITECTURE-SPINE.md).

| Объект | Значение |
|---|---|
| Subscription / tenant | `33427c73-b710-48a3-99a2-82217072bd94` / `9ee559cb-963e-4272-a38a-493dd974dd2a` |
| Agent RG / region | `rg-simple-discount-verifier-agent-develop` / West Europe |
| Foundry account / project | `aif-sdv-agent-develop-1978` / `sdv-agent-develop` |
| Project endpoint | `https://aif-sdv-agent-develop-1978.services.ai.azure.com/api/projects/sdv-agent-develop` |
| Диагностический agent | `sdv-worker-s5-smoke`; текущую версию/ZIP SHA и срок metadata проверить перед вызовом |
| Service Bus Basic | `sb-sdv-agent-develop-1978`; `input`, `results`, `commands101`, `commands102` |
| Router storage / Insights | `sdvagentrouter1978` / `appi-sdv-agent-develop` |
| Router target | Linux Flex FC1, dotnet-isolated10,2048MiB,max1 instance,no always-ready; не доказательство deploy/прав runtime |
| Web PubSub target | Free_F1, local auth disabled; точное имя/endpoint из актуального IaC/settings |
| Existing SWA | `swa-simple-discount-verifier`, RG `rg-simple-discount-verifier`, `black-pond-085834203.7.azurestaticapps.net` |
| Telemetry target | Retention30дней, daily cap1GB; cap не денежный бюджет, content capture запрещён |

Worker ZIP bundled dotnet_10 без ACR. Model deployments, cleanup Logic App, scopes и полное E2E не считаются готовыми по таблице. Loopback1С/branch, Telegram IDs и credentials — защищённые локальные настройки Connector; ни операторские PIDs, ни прежние tokens/metadata/live approvals в рабочий комплект не входят.
