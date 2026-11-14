#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$BicepPath = 'C:/Users/Pasha/.azure/bin/bicep.exe',
    [string]$ParameterFile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-ExactObject($Actual, $Expected, [string]$Path) {
    if ($Expected -is [System.Collections.IDictionary]) {
        Assert ($Actual -is [System.Collections.IDictionary] -and $Actual.get_Count() -eq $Expected.get_Count()) "Unexpected fields: $Path"
        foreach ($key in $Expected.Keys) {
            Assert ($Actual.Contains($key)) "Missing field: $Path.$key"
            Assert-ExactObject $Actual[$key] $Expected[$key] "$Path.$key"
        }
    }
    elseif ($Expected -is [array]) {
        Assert ($Actual -is [array] -and $Actual.Count -eq $Expected.Count) "Unexpected array: $Path"
        for ($i = 0; $i -lt $Expected.Count; $i++) { Assert-ExactObject $Actual[$i] $Expected[$i] "$Path[$i]" }
    }
    else { Assert ($Actual -ceq $Expected) "Unexpected expression: $Path" }
}

$infra = Split-Path $PSScriptRoot -Parent
$localRoot = Join-Path (Split-Path $infra -Parent) '.local'
$runDirectory = Join-Path $localRoot ('sdv-agent-iac-' + [guid]::NewGuid())
$previousExtractDirectory = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
try {
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $localRoot 'bundle'
    New-Item -ItemType Directory -Path $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR -Force | Out-Null
    Assert (Test-Path -LiteralPath $BicepPath -PathType Leaf) 'Installed Bicep executable is required; no installer is run.'
    $examples = @{}
    $parameterFiles = if ($ParameterFile) { @((Resolve-Path -LiteralPath $ParameterFile).Path) } else {
        @('develop', 'production') | ForEach-Object { Join-Path $infra "parameters/$_.example.json" }
    }
    foreach ($file in $parameterFiles) {
        $stage = [IO.Path]::GetFileNameWithoutExtension($file)
        $compiledPath = Join-Path $runDirectory 'main.json'
        & $BicepPath build (Join-Path $infra 'main.bicep') --no-restore --outfile $compiledPath
        Assert ($LASTEXITCODE -eq 0) "Bicep build failed: $stage"
        $arm = Get-Content -LiteralPath $compiledPath -Raw | ConvertFrom-Json -AsHashtable
        $example = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json -AsHashtable
        $p = $example.parameters
        $examples[$stage] = $p
        Assert ($example.'$schema' -eq 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#') 'Unexpected parameter schema.'
        Assert ($p.Count -eq $arm.parameters.Count) 'Parameter count differs from the template.'
        foreach ($key in $arm.parameters.Keys) {
            Assert ($p.ContainsKey($key) -and $p[$key].ContainsKey('value') -and $p[$key].Count -eq 1) "Missing/literal parameter required: $key"
            $definition = $arm.parameters[$key]
            $value = $p[$key].value
            if ($definition.ContainsKey('allowedValues')) { Assert ($value -cin $definition.allowedValues) "Disallowed value: $key" }
            if ($definition.ContainsKey('minValue')) { Assert ($value -ge $definition.minValue) "Below minimum: $key" }
            if ($definition.ContainsKey('maxValue')) { Assert ($value -le $definition.maxValue) "Above maximum: $key" }
            if ($definition.ContainsKey('type')) {
                switch ($definition.type) {
                    string { Assert ($value -is [string] -and $value.Length -gt 0) "String required: $key" }
                    int { Assert ($value -is [long] -or $value -is [int]) "Integer required: $key" }
                    array {
                        Assert ($value -is [array]) "Array required: $key"
                        foreach ($limit in @('minLength', 'minItems')) {
                            if ($definition.ContainsKey($limit)) { Assert ($value.Count -ge $definition[$limit]) "Array below minimum: $key" }
                        }
                        foreach ($limit in @('maxLength', 'maxItems')) {
                            if ($definition.ContainsKey($limit)) { Assert ($value.Count -le $definition[$limit]) "Array above maximum: $key" }
                        }
                        Assert ($definition.items.type -eq 'string') "Unsupported array item type: $key"
                        foreach ($item in $value) { Assert ($item -is [string]) "String array item required: $key" }
                    }
                }
            }
        }
        if (-not $ParameterFile) { Assert ($p.environmentName.value -ceq $stage.Replace('.example', '')) 'Wrong example environment.' }
        $names = $p.names.value
        $expectedNameKeys = $arm.definitions.ResourceNames.properties.Keys
        Assert ($names.Count -eq $expectedNameKeys.Count) 'Resource name schema differs.'
        foreach ($key in $expectedNameKeys) {
            Assert ($names.ContainsKey($key) -and $names[$key] -is [string] -and $names[$key] -match '^[a-z0-9][a-z0-9-]+[a-z0-9]$') "Invalid resource name: $key"
        }
        Assert ($names.storage -cmatch '^[a-z0-9]{3,24}$') 'Storage naming constraint.'
        Assert ($p.branchIds.value.Count -eq 2 -and @($p.branchIds.value | Sort-Object -Unique).Count -eq 2) 'Exactly two distinct branches required.'
        foreach ($branch in $p.branchIds.value) { Assert ($branch -is [string] -and $branch -cmatch '^[0-9]+$') 'Branch IDs must be numeric strings.' }
        $queueNames = @($p.inputQueueName.value, $p.resultsQueueName.value) + @($p.branchIds.value | ForEach-Object { $p.commandsQueuePrefix.value + $_ })
        Assert (@($queueNames | Sort-Object -Unique).Count -eq 4) 'Four distinct queue names required.'
        foreach ($queue in $queueNames) { Assert ($queue -cmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,259}$') 'Invalid queue name.' }
        if (-not $ParameterFile) { Assert (($queueNames -join ',') -ceq 'input,results,commands101,commands102') 'Example queue matrix differs.' }

        Assert ($arm.'$schema' -like '*subscriptionDeploymentTemplate*') 'Subscription wrapper required.'
        Assert ($arm.resources.Count -eq 2 -and $arm.resources.agentGroup.type -eq 'Microsoft.Resources/resourceGroups') 'Wrapper must only create isolated RG and local deployment.'
        $module = $arm.resources.infrastructure.properties.template
        $r = $module.resources
        $allowedTypes = @(
            'Microsoft.CognitiveServices/accounts', 'Microsoft.CognitiveServices/accounts/projects',
            'Microsoft.ServiceBus/namespaces',
            'Microsoft.ServiceBus/namespaces/queues', 'Microsoft.ServiceBus/namespaces/queues/authorizationRules',
            'Microsoft.OperationalInsights/workspaces', 'Microsoft.Insights/components',
            'Microsoft.Insights/components/CurrentBillingFeatures', 'Microsoft.Storage/storageAccounts',
            'Microsoft.Storage/storageAccounts/blobServices', 'Microsoft.Storage/storageAccounts/blobServices/containers',
            'Microsoft.Web/serverfarms', 'Microsoft.Web/sites', 'Microsoft.Resources/deployments', 'Microsoft.SignalRService/webPubSub'
        )
        foreach ($resource in $r.Values) { Assert ($resource.type -in $allowedTypes) "Unexpected resource: $($resource.type)" }
        Assert ($r.Count -eq 16 -and $r.routerAccess.type -ceq 'Microsoft.Resources/deployments') 'Unexpected resource declarations.'
        Assert ($r.foundry.kind -eq 'AIServices' -and $r.foundry.properties.allowProjectManagement -and $r.foundry.properties.disableLocalAuth) 'Foundry configuration.'
        $q = $r.queues.properties
        Assert ($q.defaultMessageTimeToLive -eq 'PT120S' -and $q.lockDuration -eq 'PT60S' -and $q.maxDeliveryCount -eq 3) 'Queue timing matrix.'
        Assert (-not $q.requiresSession -and -not $q.requiresDuplicateDetection -and -not $q.enablePartitioning) 'Basic queue constraints.'
        Assert ($r.queues.copy.count -eq "[length(variables('queueNames'))]") 'All queue names must be deployed.'
        $commandNamesLoop = @($module.variables.copy | Where-Object name -eq 'commandQueueNames')
        Assert ($module.variables.queueNames -match 'concat' -and $commandNamesLoop.Count -eq 1 -and $commandNamesLoop[0].count -eq "[length(parameters('branchIds'))]") 'Branch queue expansion.'
        Assert (($r.commandListen.properties.rights -join ',') -ceq 'Listen' -and ($r.resultSend.properties.rights -join ',') -ceq 'Send') 'Entity SAS rights.'
        Assert ($r.commandListen.name -match 'add.*2' -and $r.resultSend.name -match '\[1\]') 'SAS entity scope.'
        Assert ($r.router.kind -eq 'functionapp,linux' -and $r.router.identity.type -eq 'SystemAssigned') 'Router identity/platform.'
        Assert ($r.router.properties.functionAppConfig.runtime.name -eq 'dotnet-isolated' -and $r.router.properties.functionAppConfig.runtime.version -eq '10.0') 'Router runtime.'
        Assert ($r.router.properties.functionAppConfig.deployment.storage.authentication.type -eq 'SystemAssignedIdentity') 'Deployment must use MI.'
        Assert ($r.router.properties.functionAppConfig.scaleAndConcurrency.alwaysReady -is [array] -and $r.router.properties.functionAppConfig.scaleAndConcurrency.alwaysReady.Count -eq 0) 'Always Ready must be explicitly empty.'
        Assert (-not $r.storage.properties.allowSharedKeyAccess -and -not $r.storage.properties.allowBlobPublicAccess) 'Storage keys/public blobs forbidden.'
        Assert ($r.deploymentContainer.properties.publicAccess -eq 'None') 'Private deployment container required.'
        $access = $r.routerAccess.properties.template
        Assert ('queues' -cin $r.routerAccess.dependsOn) 'RBAC must wait for its queue scopes to exist.'
        $grants = $access.resources
        Assert ($grants.Count -eq 7) 'Unexpected nested RBAC resources.'
        foreach ($existing in @('bus', 'ingressQueues', 'storage', 'insights')) { Assert ($grants[$existing].existing -eq $true) 'RBAC scopes must reference existing resources.' }
        Assert (-not $access.ContainsKey('outputs')) 'RBAC module must not export fields.'
        Assert-ExactObject $r.routerAccess.properties.parameters @{
            routerPrincipalId = @{ value = "[reference('router', '2024-04-01', 'full').identity.principalId]" }
            serviceBusName = @{ value = "[parameters('names').serviceBus]" }
            ingressQueueNames = @{ value = @("[parameters('inputQueueName')]", "[parameters('resultsQueueName')]") }
            storageName = @{ value = "[parameters('names').storage]" }
            insightsName = @{ value = "[parameters('names').insights]" }
        } 'routerAccess.parameters'
        $receiverScope = "resourceId('Microsoft.ServiceBus/namespaces/queues', parameters('serviceBusName'), parameters('ingressQueueNames')[range(0, 2)[copyIndex()]])"
        Assert ($grants.receiverAssignments.copy.count -ceq '[length(range(0, 2))]' -and $grants.receiverAssignments.scope -ceq "[$receiverScope]") 'Receiver grants must cover exact input/results indices 0/1 only.'
        Assert ($module.variables.queueNames -ceq "[concat(createArray(parameters('inputQueueName'), parameters('resultsQueueName')), variables('commandQueueNames'))]") 'Ingress queue ordering.'
        Assert ($access.variables.serviceBusReceiverRole -ceq "[subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')]") 'Built-in Receiver role ID.'
        $roles = @($grants.Values | Where-Object type -eq 'Microsoft.Authorization/roleAssignments')
        Assert ($roles.Count -eq 3) 'Only Receiver, Blob Owner and telemetry role declarations allowed.'
        $principal = "parameters('routerPrincipalId')"
        foreach ($role in $roles) {
            Assert ($role.properties.principalId -ceq "[$principal]" -and $role.properties.principalType -ceq 'ServicePrincipal') 'Every grant must target the exact Router principal.'
        }
        Assert ($grants.receiverAssignments.name -ceq "[guid($receiverScope, $principal, variables('serviceBusReceiverRole'))]") 'Receiver GUID must include the Router principalId.'
        Assert ($grants.storageBlobRole.name -ceq "[guid(resourceId('Microsoft.Storage/storageAccounts', parameters('storageName')), $principal, 'blob-owner')]") 'Storage GUID must include the Router principalId.'
        Assert ($grants.metricsRole.name -ceq "[guid(resourceId('Microsoft.Insights/components', parameters('insightsName')), $principal, 'metrics-publisher')]") 'Telemetry GUID must include the Router principalId.'
        Assert ($grants.storageBlobRole.scope -ceq "[resourceId('Microsoft.Storage/storageAccounts', parameters('storageName'))]" -and $grants.storageBlobRole.properties.roleDefinitionId -match 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b') 'Host/deployment storage role.'
        Assert ($grants.metricsRole.scope -ceq "[resourceId('Microsoft.Insights/components', parameters('insightsName'))]" -and $grants.metricsRole.properties.roleDefinitionId -match '3913510d-42f4-4e42-8a64-420c390055eb') 'Telemetry role scope.'
        Assert ($r.insights.properties.DisableLocalAuth -and $r.insights.properties.RetentionInDays -eq 30) 'Telemetry auth/retention.'
        Assert ($r.workspace.properties.retentionInDays -ceq "[parameters('workspaceRetentionDays')]" -and $r.insights.properties.RetentionInDays -eq $p.workspaceRetentionDays.value) 'Configured telemetry retention.'
        Assert ($r.workspace.properties.workspaceCapping.dailyQuotaGb -ceq "[parameters('workspaceDailyCapGb')]" -and $r.insightsCap.properties.DataVolumeCap.Cap -ceq "[parameters('insightsDailyCapGb')]") 'Configured workspace/Insights cap expressions.'
        Assert ($r.pubSub.properties.disableLocalAuth -eq $true -and $r.pubSub.sku.name -ceq "[parameters('pubSubSku')]" -and $r.pubSub.sku.capacity -ceq "[parameters('pubSubCapacity')]") 'Configured WPS auth/SKU/capacity.'
        Assert ($r.pubSub.sku.tier -ceq "[if(equals(parameters('pubSubSku'), 'Free_F1'), 'Free', 'Standard')]") 'Configured WPS tier.'
        $settings = @{}
        foreach ($setting in $r.router.properties.siteConfig.appSettings) { $settings[$setting.name] = $setting.value }
        Assert ($settings.AZURE_TRACING_GEN_AI_CONTENT_RECORDING_ENABLED -eq 'false' -and $settings.OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT -eq 'false') 'Content capture must be disabled.'
        Assert ($settings.APPLICATIONINSIGHTS_AUTHENTICATION_STRING -eq 'Authorization=AAD' -and $settings.AzureWebJobsStorage__credential -eq 'managedidentity') 'Keyless host/telemetry settings.'
        $json = $arm | ConvertTo-Json -Depth 100 -Compress
        Assert ($json -notmatch 'Microsoft\.ContainerRegistry|registrySku|"registry"') 'ZIP delivery must not declare registry resources, parameters, names or outputs.'
        Assert ($json -notmatch 'listKeys\(|listSecrets\(|RootManageSharedAccessKey|secureString|staticSites|tableServices/tables|Microsoft.Logic|accounts/deployments') 'Forbidden secret operation or resource.'
        Assert-ExactObject $arm.outputs @{
            resourceGroupId = @{ type = 'string'; value = "[subscriptionResourceId('Microsoft.Resources/resourceGroups', parameters('resourceGroupName'))]" }
            resourceIds = @{ type = 'object'; value = "[reference('infrastructure').outputs.resourceIds.value]" }
            endpoints = @{ type = 'object'; value = "[reference('infrastructure').outputs.endpoints.value]" }
        } 'outputs'
        Assert-ExactObject $module.outputs @{
            resourceIds = @{ type = 'object'; value = @{
                foundry = "[resourceId('Microsoft.CognitiveServices/accounts', parameters('names').foundry)]"
                project = "[resourceId('Microsoft.CognitiveServices/accounts/projects', parameters('names').foundry, parameters('names').project)]"
                serviceBus = "[resourceId('Microsoft.ServiceBus/namespaces', parameters('names').serviceBus)]"
                queues = "[variables('queueIds')]"
                router = "[resourceId('Microsoft.Web/sites', parameters('names').router)]"
                storage = "[resourceId('Microsoft.Storage/storageAccounts', parameters('names').storage)]"
                deploymentContainer = "[resourceId('Microsoft.Storage/storageAccounts/blobServices/containers', parameters('names').storage, 'default', parameters('names').deploymentContainer)]"
                pubSub = "[resourceId('Microsoft.SignalRService/webPubSub', parameters('names').pubSub)]"
                workspace = "[resourceId('Microsoft.OperationalInsights/workspaces', parameters('names').workspace)]"
                insights = "[resourceId('Microsoft.Insights/components', parameters('names').insights)]"
            } }
            endpoints = @{ type = 'object'; value = @{
                foundryProject = "[format('https://{0}.services.ai.azure.com/api/projects/{1}', parameters('names').foundry, parameters('names').project)]"
                serviceBus = "[format('{0}.servicebus.windows.net', parameters('names').serviceBus)]"
                router = "[format('https://{0}', reference('router').defaultHostName)]"
                pubSub = "[format('https://{0}', reference('pubSub').hostName)]"
            } }
        } 'module.outputs'
        $queueIdsLoop = @($module.variables.copy | Where-Object name -eq 'queueIds')
        Assert ($queueIdsLoop.Count -eq 1) 'Only one queue output expansion allowed.'
        Assert-ExactObject $queueIdsLoop[0] @{
            name = 'queueIds'
            count = "[length(range(0, length(variables('queueNames'))))]"
            input = "[resourceId('Microsoft.ServiceBus/namespaces/queues', parameters('names').serviceBus, variables('queueNames')[range(0, length(variables('queueNames')))[copyIndex('queueIds')]])]"
        } 'module.variables.queueIds'
        Write-Host "PASS ${stage}: local build, parameters, resource boundaries, queues, SAS, Router, RBAC and telemetry"
    }
    if (-not $ParameterFile) {
        Assert ($examples.'develop.example'.resourceGroupName.value -ne $examples.'production.example'.resourceGroupName.value) 'Environments share RG.'
        foreach ($key in $examples.'develop.example'.names.value.Keys) {
            if ($key -ne 'deploymentContainer') { Assert ($examples.'develop.example'.names.value[$key] -ne $examples.'production.example'.names.value[$key]) "Environments share resource name: $key" }
        }
        Write-Host 'PASS environment separation.'
    }
    Write-Host 'No Azure calls, installations, secrets or runtime tests.'
}
finally {
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $previousExtractDirectory
    # Only this invocation owns this absolute, randomly named directory.
    if (Test-Path -LiteralPath $runDirectory) {
        $resolved = (Resolve-Path -LiteralPath $runDirectory).Path
        $tempRoot = [IO.Path]::GetFullPath($localRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        Assert ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and ([IO.Path]::GetFileName($resolved) -match '^sdv-agent-iac-[0-9a-f-]{36}$')) 'Unsafe temporary cleanup path.'
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
