targetScope = 'resourceGroup'

param routerPrincipalId string
param serviceBusName string
param ingressQueueNames string[]
param storageName string
param insightsName string

var serviceBusReceiverRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')
resource bus 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: serviceBusName
}
resource ingressQueues 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' existing = [for queueName in ingressQueueNames: {
  parent: bus
  name: queueName
}]
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageName
}
resource insights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: insightsName
}
resource receiverAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for i in range(0, 2): {
  name: guid(ingressQueues[i].id, routerPrincipalId, serviceBusReceiverRole)
  scope: ingressQueues[i]
  properties: { principalId: routerPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: serviceBusReceiverRole }
}]
resource storageBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, routerPrincipalId, 'blob-owner')
  scope: storage
  properties: {
    principalId: routerPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
  }
}
resource metricsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(insights.id, routerPrincipalId, 'metrics-publisher')
  scope: insights
  properties: {
    principalId: routerPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')
  }
}

