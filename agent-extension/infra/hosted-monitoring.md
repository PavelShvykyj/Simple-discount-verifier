# Подключение Hosted Worker к Application Insights

Обязательный шаг подготовки окружения после IaC и до проверки логов Worker. Документ описывает процедуру, **не подтверждает её выполнение**. Текущие целевые ресурсы — [inventory](../../_bmad-output/specs/spec-agent-extension/environment-inventory.md); запросы к логам и live gates — [Worker runbook](../../_bmad-output/specs/spec-agent-extension/worker-runbook.md). Не создавать второй Insights, если целевой ресурс уже существует.

## Граница IaC

`modules/resources.bicep` создаёт Foundry account/project, Log Analytics workspace и Application Insights с `DisableLocalAuth=true`. Connection string и AAD authentication setting в Router Function App относятся только к Router; его роль Metrics Publisher не даёт прав Worker.

Связь Foundry project → Insights, monitoring и grants Hosted Agent Identity текущие Bicep-шаблоны **не создают**. Они оформляются ниже как отдельный обязательный post-deployment шаг через документированный интерфейс Foundry. Применение одного `main.bicep` не завершает подготовку Hosted telemetry.

До изменения Azure отдельно согласовать project/Insights resource IDs, Hosted agent/version/Agent Identity object ID, требуемые role assignments и влияние на расходы/хранение. Проверять уже существующие connection/grants, не дублировать их. Документ не разрешает Azure mutations или вызов Worker.

## Подключение существующего Insights

1. Открыть нужный проект в новом Foundry portal: **Build → Agents → Traces → Connect**. Выбрать существующий целевой Application Insights и сверить resource ID.
2. Для нашего `DisableLocalAuth=true` выбрать **Auth type: Project Managed Identity** и завершить подключение.
3. Если связь уже есть, открыть **Manage → Project details**, выбрать существующее Application Insights connection и **Edit authentication → Project managed identity → Save** вместо создания второго connection. Альтернативный путь для отсутствующей связи: **Project details → Connected resources → Add connection → Application Insights**.
4. Проверить сохранённые target и authentication type. Уведомление портала об успехе ещё не доказывает доставку telemetry из Worker.

Microsoft описывает подключение существующего ресурса и автоматическое включение server-side tracing в [Foundry tracing setup](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/trace-agent-setup). Entra ingestion отдельно обозначен как preview в [инструкции Microsoft](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/trace-ingestion-entra-authentication); это не основание объявлять окружение production-ready.

## Права записи и чтения

Agent Identity создаётся и связывается с Hosted agent автоматически платформой. Перед назначением её ingestion grant получить `instance_identity.principal_id` целевого агента по [процедуре инфраструктурного развёртывания](README.md#identity-hosted-worker). Это отличается от project/Router principalId; ручной привязки произвольной MI к Worker нет.

| Identity | Назначение на связанном Application Insights |
|---|---|
| Foundry project managed identity | **Monitoring Metrics Publisher** для server-side traces; при подключении через Portal с Project Managed Identity назначение выполняет портал, его нужно проверить |
| Hosted Agent Identity | Отдельный **Monitoring Metrics Publisher** для telemetry из sandbox; проверить object ID именно агента, не Router/project identity или application client ID |
| Оператор, читающий telemetry | **Monitoring Reader** на Application Insights. Для прямых запросов к workspace — дополнительно **Log Analytics Reader** на workspace. Для защищённых таблиц — **Privileged Monitoring Data Reader**. [Microsoft](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agent-permissions#viewing-telemetry-data) |

После согласования отсутствующей роли: Application Insights → **Access control (IAM) → Add role assignment → Monitoring Metrics Publisher → Members: нужная Agent Identity → Review + assign**. Scope — конкретный Insights. Не использовать Owner/Contributor как замену data-plane роли и не отключать local-auth protection для обхода ошибки ingestion. Оператору назначения нужны права управления RBAC; право читать логи не равно праву публиковать их.

Разделение project/agent ingestion identities и права описаны в [Entra ingestion prerequisites и hosted agent setup](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/trace-ingestion-entra-authentication); роль чтения также указана в [tracing setup](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/trace-agent-setup).

## Настройки Hosted-версии

При включённом project monitoring Foundry передаёт контейнеру **`APPLICATIONINSIGHTS_CONNECTION_STRING`** связанного ресурса. Это зарезервированная настройка платформы: её не копируют из Router appsettings, не добавляют в Worker appsettings/ZIP и не переопределяют в `agent.yaml`. Установленный Hosted SDK подключает exporter автоматически. Основание — [Hosted telemetry configuration](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/configure-hosted-agent-telemetry).

Для закреплённого **Core 1.0.0-beta.29** Entra mode включается при **`APPLICATIONINSIGHTS_AUTH_MODE=Entra`**. Это контракт SDK (`FoundryEnvironment.IsAppInsightsEntraAuth`), а не придуманная настройка Worker. Перед публикацией проверить режим в настройках версии; если платформа не предоставляет его, включить в согласованные environment settings новой версии. Не подменять этим связь проекта и grants и не переносить Router `APPLICATIONINSIGHTS_AUTHENTICATION_STRING=Authorization=AAD`: это другой hosting stack.

Подтверждение для закреплённой версии: XML-doc установленного Azure.AI.AgentServer.Core 1.0.0-beta.29, свойство `FoundryEnvironment.IsAppInsightsEntraAuth`, и [настройка exporter](https://github.com/Azure/azure-sdk-for-net/blob/76fb721a87bfb536c4d3b3fb420d4e518da85983/sdk/agentserver/Azure.AI.AgentServer.Core/src/Telemetry/OpenTelemetryExtensions.cs). При Entra mode SDK использует `ManagedIdentityCredential` без выбора client ID. В Hosted это обращение к token service текущей Agent Identity, а не к project MI — см. [runtime identity](README.md#identity-hosted-worker). Реальный principal и ingestion доступ проверяются отдельно.

Environment settings версии неизменяемы после её создания: обновление требует отдельно согласованной новой версии. Worker не должен включать monitoring или выдавать себе роли при старте.

## Проверка и evidence

До запуска: подтвердить resource IDs и authentication type connection, `DisableLocalAuth=true`, отдельно grants project/Agent Identity, корректный Entra mode SDK, версию/ZIP SHA Worker и права читающего оператора. Наличие платформенной connection string проверять только как факт присутствия, без вывода значения, tokens или полного environment dump. Retention/caps/read access брать из текущих ресурсов, не считать значения примеров live readback.

В отдельно разрешённом live окне: использовать согласованную безопасную операцию, затем найти её Worker events и связанную trace/dependency в целевом Insights по [runbook](../../_bmad-output/specs/spec-agent-extension/worker-runbook.md#просмотр-логов-в-application-insights). Отдельно отличить server-side Foundry telemetry от записи из кода Worker. Проверить отсутствие исходного контента/секретов; не включать sensitive-content capture ради диагностики. Не создавать повторный бизнес-эффект для получения логов.

Зафиксировать UTC, не содержащие секретов resource/agent/version IDs, ZIP SHA, ожидаемые/полученные события, состояние доступа и ограничения проверки в verification/done-review. Connection/grants/env presence без фактической Worker-записи — **настроено, доставка не проверена**, а не PASS. При 403 проверить identity/scope/auth mode; при пустом результате — версию, связь, ingestion delay, sampling и caps, не создавать ресурсы/роли повторно вслепую.
