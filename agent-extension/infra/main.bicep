targetScope = 'subscription'

@allowed(['develop', 'production'])
param environmentName string
param location string
param resourceGroupName string
@sealed()
type ResourceNames = {
  foundry: string
  project: string
  serviceBus: string
  routerPlan: string
  router: string
  storage: string
  deploymentContainer: string
  pubSub: string
  workspace: string
  insights: string
}
param names ResourceNames
@minLength(2)
@maxLength(2)
param branchIds string[]
param inputQueueName string
param resultsQueueName string
param commandsQueuePrefix string
@allowed(['S0'])
param foundrySku string
@allowed(['Basic'])
param serviceBusSku string
@allowed(['FC1'])
param routerSku string
@allowed(['Standard_LRS'])
param storageSku string
@allowed(['Free_F1', 'Standard_S1'])
param pubSubSku string
@minValue(1)
@maxValue(1)
param pubSubCapacity int
@minValue(30)
@maxValue(30)
param workspaceRetentionDays int
@minValue(1)
@maxValue(1)
param workspaceDailyCapGb int
@minValue(1)
@maxValue(1)
param insightsDailyCapGb int

resource agentGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: {
    environment: environmentName
    workload: 'agent-extension'
  }
}

module infrastructure './modules/resources.bicep' = {
  name: 'agent-infrastructure'
  scope: agentGroup
  params: {
    environmentName: environmentName
    location: location
    names: names
    branchIds: branchIds
    inputQueueName: inputQueueName
    resultsQueueName: resultsQueueName
    commandsQueuePrefix: commandsQueuePrefix
    foundrySku: foundrySku
    serviceBusSku: serviceBusSku
    routerSku: routerSku
    storageSku: storageSku
    pubSubSku: pubSubSku
    pubSubCapacity: pubSubCapacity
    workspaceRetentionDays: workspaceRetentionDays
    workspaceDailyCapGb: workspaceDailyCapGb
    insightsDailyCapGb: insightsDailyCapGb
  }
}

output resourceGroupId string = agentGroup.id
output resourceIds object = infrastructure.outputs.resourceIds
output endpoints object = infrastructure.outputs.endpoints
