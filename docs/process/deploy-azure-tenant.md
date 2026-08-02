# Развёртывание Azure-окружения нового tenant

Эта инструкция разворачивает отдельное production-окружение Simple Discount
Verifier в Azure-аккаунте или подписке клиента. Её можно выполнять с агентом
Codex через `$deploy-azure-tenant` или самостоятельно в PowerShell.

## Контракт степера

- Перед **каждым** шагом исполнитель показывает цель, команды, изменения и
  ожидаемый результат, затем ждёт разрешение.
- Пауза обязательна даже для полностью автоматического и read-only шага.
- Один Azure write-шаг содержит не более одной изменяющей Azure-команды.
- Одно подтверждение Bicep deployment разрешает только одну показанную команду
  и только изменения из предварительно проверенного `what-if`.
- Без AI пользователь сам считается исполнителем колонки **Автоматически**, но
  не переходит к следующему шагу до проверки результата текущего.
- Секреты, connection strings и deployment token не вставляются в чат, Git,
  скриншоты или документы.

При работе с Codex обязательно используется личный skill `azure-cli-local`.
Он определяет правила Azure-авторизации, Avast и подтверждения write-команд.

## Итоговая модель

```text
develop -> master -> release/<tenant> -> Azure tenant environment
```

- `develop` продолжает разворачиваться в общей тестовой среде.
- `master` является общей стабильной базой и не разворачивает конкретного
  клиента.
- `release/<tenant>` принадлежит одному клиенту и использует отдельный Azure
  account/subscription, SWA deployment token и tenant workflow.
- Новые release-ветки не защищены текущим branch-policy, поэтому tenant
  onboarding не требует PR через `develop` или `master`.

## Необходимые входные данные

До шага 1 подготовьте:

| Значение                  | Правило                                              |
| ------------------------- | ---------------------------------------------------- |
| Tenant slug               | 3–14 строчных латинских букв, например `nakhmari`    |
| Release branch            | строго `release/<tenant-slug>`                       |
| Microsoft Entra Tenant ID | из Azure Portal                                      |
| Azure Subscription ID     | из Azure Portal                                      |
| Azure region              | по умолчанию `eastus2`; подтвердить для подписки     |
| SMS-Fly API key и sender  | из аккаунта провайдера                               |
| GitHub access             | право создавать ветки, workflow и repository secrets |
| Administrator email       | конкретный пользователь для роли SWA `admin`         |

Обычный суффикс ресурса — `-<tenant-slug>`. Для Storage Account дефис
запрещён Azure, поэтому используется `sdvstorage<tenant-slug>`.

---

## Шаг 1. Локальная проверка

**Цель:** убедиться, что команды выполняются из корня актуального репозитория.

**Автоматически:** проверить Git, Azure CLI, Bicep и рабочее дерево.

```powershell
Get-Command git, az
git status --short --branch
az version
az bicep version
```

**Пользователь:** проверить, что незакоммиченные изменения понятны и не будут
потеряны. При конфликте использовать отдельный worktree/clone.

**Изменения:** отсутствуют.

**Ожидается:** команды найдены, Bicep доступен, рабочее дерево безопасно для
создания ветки.

**Checkpoint:** `Разрешаете выполнить шаг 1?`

## Шаг 2. Azure-авторизация

**Цель:** выбрать именно tenant и subscription нового клиента.

Перед первым обращением к Azure пользователь временно отключает Avast
Web/HTTPS Shield и подтверждает это. Не изменять сертификаты и не отключать
TLS-проверку.

**Автоматически:** проверить текущую Azure CLI session.

```powershell
az account show `
  --query "{Name:name,SubscriptionId:id,TenantId:tenantId,State:state}" `
  --output table
```

Если авторизации нет:

1. Открыть терминал Codex/VS Code: `Ctrl` + `` ` ``.
2. В Azure Portal открыть **Subscriptions**, нужную подписку и скопировать
   **Subscription ID**.
3. Открыть **Microsoft Entra ID -> Overview** и скопировать **Tenant ID**.
4. Подставить значения только в локальный терминал:

```powershell
$TenantId = "<TENANT_ID>"
$SubscriptionId = "<SUBSCRIPTION_ID>"

az account clear
az config set core.enable_broker_on_windows=false
az config set core.login_experience_v2=off
az login --tenant $TenantId
az account set --subscription $SubscriptionId
az account show `
  --query "{Name:name,SubscriptionId:id,TenantId:tenantId,State:state}" `
  --output table
```

Пользователь самостоятельно завершает browser sign-in и MFA. Не использовать
`--use-device-code` после ошибки `530035` и никогда не передавать агенту пароль,
MFA code, access token или device code.

**Изменения:** только локальная Azure CLI session; Azure resources не меняются.

**Ожидается:** `Enabled`, а TenantId и SubscriptionId совпадают с входными
данными клиента.

**Checkpoint:** `Разрешаете выполнить шаг 2?`

## Шаг 3. Release-ветка и tenant-файлы

**Цель:** создать tenant release от актуального `master` и сгенерировать два
несекретных файла без ручного копирования YAML/JSON.

**Автоматически:** создать локальную ветку и запустить генератор.

```powershell
$TenantSlug = "<tenant-slug>"
$ReleaseBranch = "release/$TenantSlug"
$Location = "eastus2"
$ResourceGroup = "rg-simple-discount-verifier"
$SwaName = "swa-simple-discount-verifier-$TenantSlug"
$StorageName = "sdvstorage$TenantSlug"
$WorkspaceName = "ws-simple-discount-verifier"
$AppInsightsName = "ins-simple-discount-verifier"
$LogicAppName = "la-sdv-cleanup-$TenantSlug"
$GitHubSecretName = "AZURE_STATIC_WEB_APPS_API_TOKEN_$($TenantSlug.ToUpperInvariant())"
$ParameterFile = "infra/parameters/tenants/$TenantSlug.json"
$WorkflowFile = ".github/workflows/azure-static-web-apps-$TenantSlug.yml"

git fetch origin master
git switch master
git pull --ff-only origin master
git switch -c $ReleaseBranch

.\infra\scripts\New-TenantDeploymentFiles.ps1 `
  -TenantSlug $TenantSlug `
  -ReleaseBranch $ReleaseBranch `
  -Location $Location

git diff -- $ParameterFile $WorkflowFile
```

Если ветка уже существует, остановиться и продолжить её через
`git switch $ReleaseBranch`; не создавать новую историю поверх неё.

**Пользователь:** проверить имена, branch, регион и GitHub secret name в diff.

**Изменения:** создаётся локальная Git-ветка и два несекретных файла. Azure и
GitHub ещё не меняются.

**Ожидается:** parameter file содержит `manageStaticWebAppResource=false` и
`deployCleanupScheduler=false`; workflow слушает только release-ветку.

**Checkpoint:** `Разрешаете выполнить шаг 3?`

## Шаг 4. Azure inventory и providers

**Цель:** не предполагать, что новый account пуст или providers зарегистрированы.

**Автоматически:** прочитать inventory и статусы обязательных providers.

```powershell
az group list `
  --subscription $SubscriptionId `
  --query "[].{Name:name,Location:location,State:properties.provisioningState}" `
  --output table

az resource list `
  --subscription $SubscriptionId `
  --query "[].{Name:name,Type:type,ResourceGroup:resourceGroup,Location:location}" `
  --output table

az provider list `
  --subscription $SubscriptionId `
  --query "[?namespace=='Microsoft.Storage' || namespace=='Microsoft.Web' || namespace=='Microsoft.OperationalInsights' || namespace=='Microsoft.Insights' || namespace=='Microsoft.Logic'].{Namespace:namespace,State:registrationState}" `
  --output table
```

**Пользователь:** подтвердить, что inventory принадлежит нужному клиенту.

**Изменения:** отсутствуют.

**Ожидается:** известен список отсутствующих providers и существующих ресурсов.

**Checkpoint:** `Разрешаете выполнить шаг 4?`

## Шаг 5. Регистрация отсутствующих providers

**Цель:** зарегистрировать только providers со статусом `NotRegistered`.

Для каждого отсутствующего namespace выполняется отдельный подшаг и требуется
отдельное подтверждение:

```powershell
az provider register `
  --namespace <MISSING_NAMESPACE> `
  --subscription $SubscriptionId `
  --wait
```

Возможные namespaces:

- `Microsoft.Storage`;
- `Microsoft.Web`;
- `Microsoft.OperationalInsights`;
- `Microsoft.Insights`;
- `Microsoft.Logic`.

**Изменения:** один provider регистрируется в выбранной subscription.

**Ожидается:** команда `az provider show --namespace <namespace>` возвращает
`Registered`.

**Checkpoint для каждого:**
`Разрешаете зарегистрировать provider <MISSING_NAMESPACE>?`

Если все providers уже зарегистрированы, шаг завершается без write-команд.

## Шаг 6. Проверка имён

**Цель:** проверить Azure naming constraints до deployment.

**Автоматически:** проверить глобально уникальное имя Storage Account и наличие
одноимённых ресурсов в subscription.

```powershell
az storage account check-name `
  --name $StorageName `
  --subscription $SubscriptionId `
  --query "{Available:nameAvailable,Reason:reason,Message:message}" `
  --output table

az resource list `
  --subscription $SubscriptionId `
  --query "[?name=='$SwaName' || name=='$StorageName'].{Name:name,Type:type,ResourceGroup:resourceGroup}" `
  --output table
```

**Пользователь:** при конфликте выбрать другой slug из латинских букв и
повторить шаг 3. Не добавлять дефис в Storage Account.

**Изменения:** отсутствуют.

**Ожидается:** `Available=True`, а inventory не содержит конфликтов.

**Checkpoint:** `Разрешаете выполнить шаг 6?`

## Шаг 7. Resource Group

**Цель:** создать scope для tenant-ресурсов.

**Автоматически:** выполнить одну Azure write-команду.

```powershell
az group create `
  --name $ResourceGroup `
  --location $Location `
  --subscription $SubscriptionId `
  --query "{Name:name,Location:location,State:properties.provisioningState}" `
  --output table
```

**Изменения:** создаётся или идемпотентно подтверждается Resource Group.

**Ожидается:** `Succeeded`. Location группы является метаданными и не заставляет
все вложенные ресурсы использовать тот же регион.

**Checkpoint:** `Разрешаете создать Resource Group $ResourceGroup?`

## Шаг 8. Пустая Static Web App

**Цель:** создать SWA без GitHub-интеграции, чтобы Azure не создавал workflow и
не изменял ветки репозитория.

**Автоматически:** выполнить одну Azure write-команду.

```powershell
az staticwebapp create `
  --name $SwaName `
  --resource-group $ResourceGroup `
  --location $Location `
  --sku Free `
  --subscription $SubscriptionId `
  --query "{Name:name,Location:location,Sku:sku.name,Hostname:defaultHostname}" `
  --output table
```

**Изменения:** создаётся пустая Free SWA без source-control integration.

**Ожидается:** ресурс создан и имеет default hostname. Если Azure возвращает
`RequestDisallowedByAzure`, это ограничение subscription/location, а не запрос
перехода на платный SKU. Сначала проверить account и policy, затем выбрать
регион из текста Azure error; `eastus2` является текущим проверенным fallback.

**Checkpoint:** `Разрешаете создать SWA $SwaName в $Location?`

## Шаг 9. Bicep build и what-if

**Цель:** увидеть полный foundation diff до Azure deployment.

**Автоматически:** проверить Bicep локально и выполнить read-only `what-if`.

```powershell
az bicep build --file infra/main.bicep --stdout | Out-Null

az deployment group what-if `
  --subscription $SubscriptionId `
  --resource-group $ResourceGroup `
  --template-file infra/main.bicep `
  --parameters "@$ParameterFile" `
  --result-format ResourceIdOnly
```

**Пользователь:** подтвердить ожидаемые ресурсы:

- Storage Account и четыре Table Storage tables;
- Log Analytics workspace;
- Application Insights и `CurrentBillingFeatures`;
- SWA помечена Ignore/NoChange, потому что создавалась отдельно;
- Logic App пока отсутствует.

**Изменения:** отсутствуют.

**Checkpoint:** `Разрешаете выполнить Bicep build и what-if?`

## Шаг 10. Foundation deployment

**Цель:** применить только проверенный diff шага 9.

**Автоматически:** выполнить одну Azure write-команду.

```powershell
az deployment group create `
  --name "tenant-$TenantSlug-foundation" `
  --subscription $SubscriptionId `
  --resource-group $ResourceGroup `
  --template-file infra/main.bicep `
  --parameters "@$ParameterFile" `
  --query "{Name:name,State:properties.provisioningState}" `
  --output table
```

**Изменения:** создаются ресурсы, перечисленные в одобренном `what-if`.

**Ожидается:** deployment `Succeeded`.

**Checkpoint:** `Разрешаете применить foundation deployment?`

## Шаг 11. Runtime settings и secrets

**Цель:** одной Azure write-командой настроить managed API без записи секретов
в Git или историю команд.

Не закрывать PowerShell после этого шага: `$cleanupAutomationKey` понадобится
для Logic App. Если терминал закрыт, сгенерировать новый ключ, повторно обновить
SWA setting и только затем продолжать.

**Автоматически:** получить connection strings в переменные и сгенерировать
tenant-specific secrets.

```powershell
function New-RandomSecret {
  $bytes = New-Object byte[] 32
  $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
  try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
  [Convert]::ToBase64String($bytes)
}

function Read-HiddenSecret([string]$Prompt) {
  $secure = Read-Host $Prompt -AsSecureString
  $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
  try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
  finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

$storageConnectionString = az storage account show-connection-string `
  --name $StorageName `
  --resource-group $ResourceGroup `
  --subscription $SubscriptionId `
  --query connectionString `
  --output tsv

$applicationInsightsConnectionString = az monitor app-insights component show `
  --app $AppInsightsName `
  --resource-group $ResourceGroup `
  --subscription $SubscriptionId `
  --query connectionString `
  --output tsv

$posMainClientHmacSecret = New-RandomSecret
$phoneRuntimeKeySecret = New-RandomSecret
$smsCodeHashSecret = New-RandomSecret
$barcodeHashSecret = New-RandomSecret
$auditPhoneHashSecret = New-RandomSecret
$cleanupAutomationKey = New-RandomSecret
$smsFlyApiKey = Read-HiddenSecret 'SMS-Fly API key'
$smsFlySender = Read-Host 'SMS-Fly sender'
```

**Пользователь:** ввести SMS-Fly values локально и сохранить POS HMAC secret в
одобренном secret manager для последующей настройки POS.

После проверки заполненности агент показывает следующую единственную
state-changing команду и отдельно получает разрешение:

```powershell
az staticwebapp appsettings set `
  --name $SwaName `
  --resource-group $ResourceGroup `
  --subscription $SubscriptionId `
  --setting-names `
    APPLICATIONINSIGHTS_CONNECTION_STRING="$applicationInsightsConnectionString" `
    AppStorageConnectionString="$storageConnectionString" `
    ScannerSurveyTableName=ScannerSurveyResults `
    CustomerProfilesTableName=CustomerProfiles `
    DiscountRuntimeTableName=DiscountRuntime `
    AuditEventsTableName=AuditEvents `
    PosMainClientId=main-pos-system `
    PosMainClientHmacSecret="$posMainClientHmacSecret" `
    PosRequestFreshnessToleranceSeconds=300 `
    PhoneRuntimeKeySecret="$phoneRuntimeKeySecret" `
    SmsCodeHashSecret="$smsCodeHashSecret" `
    BarcodeHashSecret="$barcodeHashSecret" `
    AuditPhoneHashSecret="$auditPhoneHashSecret" `
    CleanupAutomationKey="$cleanupAutomationKey" `
    SmsFlyApiKey="$smsFlyApiKey" `
    SmsFlySender="$smsFlySender" `
    SmsCodeTtlSeconds=180 `
    SmsRetryAfterSeconds=180 `
    SmsMaxPerHour=5 `
    SmsMaxPerDay=10 `
    SmsResponseFloorMilliseconds=1500 `
    BarcodeTtlSeconds=180 `
    DiscountRuntimeRetentionHours=24 `
    AuditEventsRetentionDays=30 `
  --output none
```

**Изменения:** добавляются/обновляются перечисленные SWA app settings.

**Ожидается:** команда успешна; проверка выводит только имена:

```powershell
$settings = az staticwebapp appsettings list `
  --name $SwaName `
  --resource-group $ResourceGroup `
  --subscription $SubscriptionId `
  --output json | ConvertFrom-Json
$settings.properties.PSObject.Properties.Name | Sort-Object
```

Azure CLI маскирует значения как `null`; это не означает, что они пусты.

**Checkpoint:** сначала разрешение на read/generate-подшаг, затем отдельное
`Разрешаете обновить runtime settings SWA $SwaName?`

## Шаг 12. GitHub deployment secret

**Цель:** передать deployment token из Azure в GitHub без вывода токена.

**Автоматически:** скопировать token только в clipboard.

```powershell
$deploymentToken = az staticwebapp secrets list `
  --name $SwaName `
  --resource-group $ResourceGroup `
  --subscription $SubscriptionId `
  --query properties.apiKey `
  --output tsv
Set-Clipboard -Value $deploymentToken
Remove-Variable deploymentToken
```

**Пользователь:** открыть GitHub repository:

1. **Settings -> Secrets and variables -> Actions**.
2. Нажать **New repository secret**.
3. Name: значение `$GitHubSecretName`.
4. Secret: вставить clipboard.
5. Сохранить и очистить clipboard: `Set-Clipboard -Value ''`.

**Изменения:** создаётся GitHub Actions repository secret; Azure не меняется.

**Ожидается:** secret name виден в GitHub без отображения значения.

**Checkpoint:** `Разрешаете получить deployment token для ручного добавления в GitHub?`

## Шаг 13. Первый push и deployment

**Цель:** опубликовать tenant branch и запустить только tenant workflow.

**Автоматически:** проверить diff, commit и push.

```powershell
git diff --check
git add $ParameterFile $WorkflowFile
git commit -m "ci: add $TenantSlug tenant deployment"
git push -u origin $ReleaseBranch
```

**Пользователь:** открыть GitHub **Actions** и проверить workflow
`Azure Static Web Apps CI/CD - <tenant-slug>`.

**Изменения:** создаётся remote release-ветка; push запускает SWA deployment.

**Ожидается:** job `Build and Deploy <tenant-slug>` завершён успешно.

**Checkpoint:** `Разрешаете commit и push release-ветки?`

## Шаг 14. Smoke tests

**Цель:** подтвердить frontend и managed API, а не только зелёный workflow.

**Автоматически:** получить hostname и выполнить read-only HTTP checks.

```powershell
$SwaHostname = az staticwebapp show `
  --name $SwaName `
  --resource-group $ResourceGroup `
  --subscription $SubscriptionId `
  --query defaultHostname `
  --output tsv

$frontend = Invoke-WebRequest -Uri "https://$SwaHostname/"
$health = Invoke-RestMethod -Uri "https://$SwaHostname/api/system/health"

[pscustomobject]@{
  Hostname = $SwaHostname
  FrontendStatus = $frontend.StatusCode
  ApiStatus = $health.status
}
```

**Пользователь:** открыть сайт, проверить tenant branding и один реальный
бизнес-flow. Favicon/PWA icon могут потребовать hard refresh или очистку cache.

**Изменения:** отсутствуют.

**Ожидается:** frontend `200`, API `ok`, бизнес-flow работает.

**Checkpoint:** `Разрешаете выполнить smoke tests?`

## Шаг 15. Smoke test cleanup endpoint

**Цель:** проверить ключ и endpoint до создания расписания.

Это state-changing POST: endpoint удаляет только записи старше серверных
retention settings. Для нового окружения таблицы должны быть пустыми; всё равно
показать target и получить отдельное подтверждение.

```powershell
$cleanupResponse = Invoke-RestMethod `
  -Method Post `
  -Uri "https://$SwaHostname/api/system/maintenance/cleanup" `
  -Headers @{ 'x-cleanup-key' = $cleanupAutomationKey } `
  -ContentType 'application/json' `
  -Body '{}'

$cleanupResponse | ConvertTo-Json -Depth 5
```

**Изменения:** удаляются только просроченные `DiscountRuntime` и `AuditEvents`.

**Ожидается:** успешный aggregate response без customer payload и secrets.

**Checkpoint:** `Разрешаете вызвать cleanup endpoint нового окружения?`

## Шаг 16. Cleanup parameter update и what-if

**Цель:** включить scheduler только после успешного шага 15.

**Автоматически:** изменить два несекретных parameter values и выполнить
read-only `what-if`.

```powershell
$parameters = Get-Content -Raw -Encoding UTF8 $ParameterFile | ConvertFrom-Json
$parameters.parameters.deployCleanupScheduler.value = $true
$parameters.parameters.cleanupEndpointUrl.value = "https://$SwaHostname/api/system/maintenance/cleanup"
$json = ($parameters | ConvertTo-Json -Depth 20) + [Environment]::NewLine
[System.IO.File]::WriteAllText(
  (Resolve-Path $ParameterFile).Path,
  $json,
  (New-Object System.Text.UTF8Encoding($false))
)

git diff -- $ParameterFile

az deployment group what-if `
  --subscription $SubscriptionId `
  --resource-group $ResourceGroup `
  --template-file infra/main.bicep `
  --parameters "@$ParameterFile" `
  --parameters cleanupAutomationKey="$cleanupAutomationKey" `
  --result-format ResourceIdOnly
```

**Пользователь:** убедиться, что единственное существенное создание —
`Microsoft.Logic/workflows/$LogicAppName`.

**Изменения:** локально обновляется parameter file; Azure не меняется.

**Checkpoint:** `Разрешаете включить cleanup в parameter file и выполнить what-if?`

## Шаг 17. Cleanup scheduler deployment

**Цель:** применить проверенный cleanup diff.

**Автоматически:** выполнить одну Azure write-команду.

```powershell
az deployment group create `
  --name "tenant-$TenantSlug-cleanup" `
  --subscription $SubscriptionId `
  --resource-group $ResourceGroup `
  --template-file infra/main.bicep `
  --parameters "@$ParameterFile" `
  --parameters cleanupAutomationKey="$cleanupAutomationKey" `
  --query "{Name:name,State:properties.provisioningState,LogicApp:properties.outputs.cleanupLogicAppName.value}" `
  --output table
```

**Изменения:** создаётся включённая Logic App с secure inputs/outputs.

**Ожидается:** deployment `Succeeded`, LogicApp равна `$LogicAppName`.

**Checkpoint:** `Разрешаете развернуть cleanup scheduler?`

## Шаг 18. Logic App run и сохранение параметров

**Цель:** проверить реальный scheduled caller и сохранить несекретное состояние.

Ручной trigger является state-changing действием и требует отдельного
подтверждения:

```powershell
$logicAppResource = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Logic/workflows/$LogicAppName"

az rest `
  --method post `
  --url "$logicAppResource/triggers/schedule/run?api-version=2016-06-01" `
  --output none
```

После выполнения прочитать последний run. Конкатенация строки сохраняет
`$top` как query parameter и не даёт PowerShell интерпретировать его:

```powershell
$runsUrl = $logicAppResource + '/runs?api-version=2016-06-01&$top=1'
az rest `
  --method get `
  --url $runsUrl `
  --query "value[0].{Run:name,Status:properties.status,Started:properties.startTime,Ended:properties.endTime}" `
  --output table
```

Затем сохранить только parameter change:

```powershell
git add $ParameterFile
git commit -m "infra: enable $TenantSlug cleanup scheduler"
git push
```

**Изменения:** запускается один Logic App run и обновляется release-ветка.

**Ожидается:** run `Succeeded`; последующий GitHub workflow также зелёный.

**Checkpoint:** сначала разрешение на manual trigger, затем отдельное разрешение
на commit/push parameter change.

## Шаг 19. Administrator и финальная проверка

**Цель:** завершить эксплуатационную готовность.

**Пользователь:** в Azure Portal открыть SWA -> **Role management / Users ->
Invite**, указать конкретный email и роль `admin`; принять invitation этим
пользователем. Это custom SWA role, а не Azure RBAC role.

**Автоматически:** после отдельного разрешения выполнить финальный read-only
inventory без вывода секретных значений.

```powershell
az resource list `
  --subscription $SubscriptionId `
  --resource-group $ResourceGroup `
  --query "[].{Name:name,Type:type,Location:location}" `
  --output table

az storage table list `
  --account-name $StorageName `
  --auth-mode login `
  --subscription $SubscriptionId `
  --query "[].name" `
  --output table

$settings = az staticwebapp appsettings list `
  --name $SwaName `
  --resource-group $ResourceGroup `
  --subscription $SubscriptionId `
  --output json | ConvertFrom-Json
$settings.properties.PSObject.Properties.Name | Sort-Object
```

После проверки удалить plain-text secret variables из текущей PowerShell
session:

```powershell
Remove-Variable `
  storageConnectionString, applicationInsightsConnectionString, `
  posMainClientHmacSecret, phoneRuntimeKeySecret, smsCodeHashSecret, `
  barcodeHashSecret, auditPhoneHashSecret, cleanupAutomationKey, `
  smsFlyApiKey, smsFlySender `
  -ErrorAction SilentlyContinue
```

После последней Azure-команды пользователь немедленно включает Avast shields и
подтверждает восстановление защиты.

**Ожидается:** все ресурсы, четыре таблицы, runtime setting names, admin access,
зелёный workflow и работающий cleanup подтверждены.

**Checkpoint:** отдельное подтверждение invitation, inventory и включения Avast.

## Необязательные расширения

Выполняются отдельными степерами только по явному запросу:

- custom domain и DNS;
- Standard SWA SKU;
- alert email receivers и scheduled query alerts;
- дополнительные administrators;
- импорт начальных CustomerProfiles;
- изменение retention или cleanup schedule.

## Типичные ошибки

| Ошибка                                  | Причина и действие                                                                                                |
| --------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `Invalid selection` во время `az login` | Команда введена внутрь selector. Сначала выбрать номер subscription/нажать Enter и дождаться `PS ...>`            |
| `530035`                                | Device-code flow заблокирован Security Defaults. Использовать browser login из шага 2                             |
| `CERTIFICATE_VERIFY_FAILED`             | Проверить Avast Web/HTTPS Shield. Не отключать TLS verification и не менять certificates                          |
| `SubscriptionNotFound`                  | Проверить TenantId/SubscriptionId и provider registration; не менять pricing вслепую                              |
| `RequestDisallowedByAzure`              | Subscription/location eligibility. Проверить error и выбрать разрешённый регион; это не обязательно pricing issue |
| `LocationNotAvailableForResourceType`   | Выбранный регион не поддерживает SWA; использовать регион из Azure error                                          |
| `AccountNameInvalid`                    | Storage name допускает только 3–24 строчных букв/цифр без дефисов; использовать генератор                         |
| GitHub `409 Repository rule violations` | Не разрешать Azure создавать workflow. Создавать пустую SWA и использовать tenant deployment token                |
| OIDC workflow filename error            | Использовать сгенерированный deployment-token workflow и правильный repository secret                             |
| `ResourceNotFound` после SWA create     | Предыдущая операция создания не завершилась; сначала проверить `az staticwebapp list`                             |
| PowerShell выполняет `$top` как команду | Формировать URL через конкатенацию как в шаге 18                                                                  |

## Возобновление после паузы

Не повторять write-команды автоматически. Сначала определить последний
успешный checkpoint через Git status, Azure inventory, deployment history и
GitHub Actions. Затем показать следующий незавершённый шаг и запросить
разрешение.

## Официальные ссылки

- Azure CLI Static Web Apps: https://learn.microsoft.com/cli/azure/staticwebapp
- SWA application settings: https://learn.microsoft.com/azure/static-web-apps/application-settings
- External deployment providers/token: https://learn.microsoft.com/azure/static-web-apps/external-providers
- Deployment token management: https://learn.microsoft.com/azure/static-web-apps/deployment-token-management
