targetScope = 'resourceGroup'

param environmentName string
param location string
param names object
param branchIds string[]
param inputQueueName string
param resultsQueueName string
param commandsQueuePrefix string
param foundrySku string
param serviceBusSku string
param routerSku string
param storageSku string
param pubSubSku string
param pubSubCapacity int
param workspaceRetentionDays int
param workspaceDailyCapGb int
param insightsDailyCapGb int

var tags = { environment: environmentName, workload: 'agent-extension' }
var commandQueueNames = [for branch in branchIds: '${commandsQueuePrefix}${branch}']
var queueNames = concat([inputQueueName, resultsQueueName], commandQueueNames)

resource foundry 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: names.foundry
  location: location
  tags: tags
  kind: 'AIServices'
  sku: { name: foundrySku }
  identity: { type: 'SystemAssigned' }
  properties: {
    allowProjectManagement: true
    customSubDomainName: names.foundry
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}
resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: foundry
  name: names.project
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: { displayName: names.project }
}
resource bus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: names.serviceBus
  location: location
  tags: tags
  sku: { name: serviceBusSku, tier: serviceBusSku }
  properties: { minimumTlsVersion: '1.2', publicNetworkAccess: 'Enabled', disableLocalAuth: false }
}
resource queues 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = [for queueName in queueNames: {
  parent: bus
  name: queueName
  properties: {
    defaultMessageTimeToLive: 'PT120S'
    lockDuration: 'PT60S'
    maxDeliveryCount: 3
    requiresSession: false
    requiresDuplicateDetection: false
    deadLetteringOnMessageExpiration: true
    enablePartitioning: false
    maxSizeInMegabytes: 1024
  }
}]
resource commandListen 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2024-01-01' = [for (branch, i) in branchIds: {
  parent: queues[i + 2]
  name: 'connector-${branch}-listen'
  properties: { rights: ['Listen'] }
}]
resource resultSend 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2024-01-01' = [for branch in branchIds: {
  parent: queues[1]
  name: 'connector-${branch}-send'
  properties: { rights: ['Send'] }
}]
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: names.workspace
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: workspaceRetentionDays
    workspaceCapping: { dailyQuotaGb: workspaceDailyCapGb }
  }
}
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: names.insights
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    DisableLocalAuth: true
    RetentionInDays: 30
    IngestionMode: 'LogAnalytics'
  }
}
resource insightsCap 'Microsoft.Insights/components/CurrentBillingFeatures@2015-05-01' = {
  parent: insights
  name: 'currentBillingFeatures'
  properties: {
    CurrentBillingFeatures: ['Basic']
    DataVolumeCap: { Cap: insightsDailyCapGb, StopSendNotificationWhenHitCap: false }
  }
}
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: names.storage
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: storageSku }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    publicNetworkAccess: 'Enabled'
  }
}
resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}
resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobs
  name: names.deploymentContainer
  properties: { publicAccess: 'None' }
}
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: names.routerPlan
  location: location
  tags: tags
  kind: 'functionapp'
  sku: { name: routerSku, tier: 'FlexConsumption' }
  properties: { reserved: true }
}
resource router 'Microsoft.Web/sites@2024-04-01' = {
  name: names.router
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    functionAppConfig: {
      runtime: { name: 'dotnet-isolated', version: '10.0' }
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainer.name}'
          authentication: { type: 'SystemAssignedIdentity' }
        }
      }
      scaleAndConcurrency: { maximumInstanceCount: 1, instanceMemoryMB: 2048, alwaysReady: [] }
    }
    siteConfig: {
      alwaysOn: false
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__blobServiceUri', value: storage.properties.primaryEndpoints.blob }
        { name: 'AzureWebJobsStorage__queueServiceUri', value: storage.properties.primaryEndpoints.queue }
        { name: 'AzureWebJobsStorage__tableServiceUri', value: storage.properties.primaryEndpoints.table }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
        { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'Authorization=AAD' }
        { name: 'AZURE_TRACING_GEN_AI_CONTENT_RECORDING_ENABLED', value: 'false' }
        { name: 'OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT', value: 'false' }
      ]
    }
  }
}
// The nested deployment resolves principalId before calculating immutable assignment names.
module routerAccess './router-access.bicep' = {
  name: 'router-access'
  dependsOn: [queues]
  params: {
    routerPrincipalId: router.identity.principalId
    serviceBusName: bus.name
    ingressQueueNames: [inputQueueName, resultsQueueName]
    storageName: storage.name
    insightsName: insights.name
  }
}
resource pubSub 'Microsoft.SignalRService/webPubSub@2024-03-01' = {
  name: names.pubSub
  location: location
  tags: tags
  sku: { name: pubSubSku, tier: pubSubSku == 'Free_F1' ? 'Free' : 'Standard', capacity: pubSubCapacity }
  properties: {
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
    tls: { clientCertEnabled: false }
  }
}

var queueIds = [for i in range(0, length(queueNames)): queues[i].id]
output resourceIds object = {
  foundry: foundry.id
  project: project.id
  serviceBus: bus.id
  queues: queueIds
  router: router.id
  storage: storage.id
  deploymentContainer: deploymentContainer.id
  pubSub: pubSub.id
  workspace: workspace.id
  insights: insights.id
}
output endpoints object = {
  foundryProject: 'https://${foundry.name}.services.ai.azure.com/api/projects/${project.name}'
  serviceBus: '${bus.name}.servicebus.windows.net'
  router: 'https://${router.properties.defaultHostName}'
  pubSub: 'https://${pubSub.properties.hostName}'
}

