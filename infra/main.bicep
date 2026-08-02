targetScope = 'resourceGroup'

@description('Short environment name, for example develop or production.')
param environmentName string

@description('Azure region for regional resources.')
param location string = resourceGroup().location

@description('Azure Static Web App name.')
param staticWebAppName string

@description('Whether this deployment should create or update the Static Web App resource. Keep false for already connected environments unless SWA resource drift is intentional.')
param manageStaticWebAppResource bool = false

@description('Azure Static Web App SKU name.')
@allowed([
  'Free'
  'Standard'
])
param staticWebAppSkuName string = 'Free'

@description('GitHub repository URL connected to the Static Web App.')
param repositoryUrl string

@description('Git branch connected to the Static Web App.')
param branch string

@description('Static Web Apps app source path.')
param appLocation string = './frontend'

@description('Static Web Apps managed API source path.')
param apiLocation string = './functions'

@description('Static Web Apps frontend build output path.')
param outputLocation string = '/dist/app/browser'

@secure()
@description('GitHub repository token for creating or reconnecting the Static Web App. Leave empty when updating an already connected SWA.')
param repositoryToken string = ''

@description('Storage account name. Must be globally unique for new environments.')
param storageAccountName string

@description('Storage account SKU.')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
  'Standard_ZRS'
])
param storageSkuName string = 'Standard_LRS'

@description('Azure Table names required by the application.')
param tableNames array = [
  'ScannerSurveyResults'
  'CustomerProfiles'
  'DiscountRuntime'
  'AuditEvents'
]

@description('Log Analytics workspace name for workspace-based Application Insights.')
param logAnalyticsWorkspaceName string

@description('Application Insights component name.')
param appInsightsName string

@description('Application Insights retention in days.')
param appInsightsRetentionInDays int = 30

@description('Application Insights daily cap in GB. Use "0.1" for 100 MB/day.')
param appInsightsDailyCapGB string = '0.1'

@description('Application Insights daily cap warning threshold percentage.')
param appInsightsDailyCapWarningThreshold int = 90

@description('Whether Application Insights stops sending cap-hit notifications after the cap is reached.')
param appInsightsStopSendNotificationWhenHitCap bool = false

@description('Whether to manage Static Web Apps app settings from this deployment. Keep false until all secure values are supplied.')
param manageStaticWebAppSettings bool = false

@description('AuditEvents retention in days for backend cleanup.')
param auditEventsRetentionDays int = 30

@secure()
@description('Storage connection string for backend Azure Tables. Required only when manageStaticWebAppSettings is true.')
param appStorageConnectionString string = ''

@secure()
@description('Application Insights connection string. Required only when manageStaticWebAppSettings is true and the hosting model does not inject it.')
param applicationInsightsConnectionString string = ''

@description('Scanner survey table name used by the existing function.')
param scannerSurveyTableName string = 'ScannerSurveyResults'

@description('Customer profiles table name for future backend APIs.')
param customerProfilesTableName string = 'CustomerProfiles'

@description('Discount runtime table name for future backend APIs.')
param discountRuntimeTableName string = 'DiscountRuntime'

@description('Audit events table name for future backend APIs.')
param auditEventsTableName string = 'AuditEvents'

@secure()
@description('POS HMAC secret for main-pos-system. Required only when manageStaticWebAppSettings is true and POS APIs are enabled.')
param mainPosSystemHmacSecret string = ''

@secure()
@description('Secret used to derive deterministic opaque phone runtime keys.')
param phoneRuntimeKeySecret string = ''

@secure()
@description('Secret used to hash SMS codes.')
param smsCodeHashSecret string = ''

@secure()
@description('Secret used to hash barcode values.')
param barcodeHashSecret string = ''

@secure()
@description('Secret used to hash phone values in AuditEvents.')
param auditPhoneHashSecret string = ''

@secure()
@description('SMS-Fly API key or token. Required only when manageStaticWebAppSettings is true and SMS sending is enabled.')
param smsFlyApiKey string = ''

@secure()
@description('Shared secret for the Logic App maintenance cleanup endpoint. Required only when enabling the cleanup scheduler or managing related SWA app settings.')
param cleanupAutomationKey string = ''

@description('SMS-Fly sender name or id.')
param smsFlySender string = ''

@minValue(1)
@description('Maximum SMS sends allowed per phone in a fixed one-hour window.')
param smsMaxPerHour int = 5

@minValue(1)
@description('Maximum SMS sends allowed per phone in a fixed 24-hour window.')
param smsMaxPerDay int = 10

@minValue(1)
@description('Minimum public SMS-start response duration in milliseconds.')
param smsResponseFloorMilliseconds int = 1500

@description('Additional non-secret Static Web Apps app settings to merge when manageStaticWebAppSettings is true.')
param additionalAppSettings object = {}

@description('Whether to create or update the Logic App scheduled cleanup workflow.')
param deployCleanupScheduler bool = false

@description('Logic App workflow name for scheduled cleanup. Leave empty to use a name derived from the Static Web App.')
param cleanupLogicAppName string = ''

@description('Full URL of the maintenance cleanup endpoint, for example https://<host>/api/system/maintenance/cleanup.')
param cleanupEndpointUrl string = ''

@description('Cleanup scheduler recurrence frequency.')
@allowed([
  'Minute'
  'Hour'
  'Day'
  'Week'
  'Month'
])
param cleanupScheduleFrequency string = 'Day'

@description('Cleanup scheduler recurrence interval.')
param cleanupScheduleInterval int = 1

@description('Email receivers for production-pilot alert action group. Example item: { name: "ops", email: "ops@example.com" }.')
param alertEmailReceivers array = []

@description('Create scheduled query alert rules. Alert rules are only created when at least one email receiver is supplied.')
param enableScheduledQueryAlerts bool = false

var staticWebAppProperties = union({
  repositoryUrl: repositoryUrl
  branch: branch
  provider: 'GitHub'
  stagingEnvironmentPolicy: 'Enabled'
  buildProperties: {
    appLocation: appLocation
    apiLocation: apiLocation
    outputLocation: outputLocation
  }
}, empty(repositoryToken) ? {} : {
  repositoryToken: repositoryToken
})

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = if (manageStaticWebAppResource) {
  name: staticWebAppName
  location: location
  sku: {
    name: staticWebAppSkuName
    tier: staticWebAppSkuName
  }
  properties: staticWebAppProperties
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: storageSkuName
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource storageTables 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = [for tableName in tableNames: {
  parent: tableService
  name: tableName
}]

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: appInsightsRetentionInDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    RetentionInDays: appInsightsRetentionInDays
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource appInsightsBilling 'Microsoft.Insights/components/CurrentBillingFeatures@2015-05-01' = {
  parent: appInsights
  name: 'currentBillingFeatures'
  properties: {
    CurrentBillingFeatures: [
      'Basic'
    ]
    DataVolumeCap: {
      Cap: json(appInsightsDailyCapGB)
      WarningThreshold: appInsightsDailyCapWarningThreshold
      StopSendNotificationWhenHitCap: appInsightsStopSendNotificationWhenHitCap
    }
  }
}

var managedAppSettings = union({
  APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsightsConnectionString
  AppStorageConnectionString: appStorageConnectionString
  ScannerSurveyTableName: scannerSurveyTableName
  CustomerProfilesTableName: customerProfilesTableName
  DiscountRuntimeTableName: discountRuntimeTableName
  AuditEventsTableName: auditEventsTableName
  PosMainClientId: 'main-pos-system'
  PosMainClientHmacSecret: mainPosSystemHmacSecret
  PhoneRuntimeKeySecret: phoneRuntimeKeySecret
  SmsCodeHashSecret: smsCodeHashSecret
  BarcodeHashSecret: barcodeHashSecret
  AuditPhoneHashSecret: auditPhoneHashSecret
  AuditEventsRetentionDays: string(auditEventsRetentionDays)
  CleanupAutomationKey: cleanupAutomationKey
  SmsFlyApiKey: smsFlyApiKey
  SmsFlySender: smsFlySender
  SmsMaxPerHour: string(smsMaxPerHour)
  SmsMaxPerDay: string(smsMaxPerDay)
  SmsResponseFloorMilliseconds: string(smsResponseFloorMilliseconds)
}, additionalAppSettings)

resource staticWebAppSettings 'Microsoft.Web/staticSites/config@2023-12-01' = if (manageStaticWebAppResource && manageStaticWebAppSettings) {
  parent: staticWebApp
  name: 'appsettings'
  properties: managedAppSettings
}

var resolvedCleanupLogicAppName = empty(cleanupLogicAppName)
  ? '${staticWebAppName}-cleanup-scheduler'
  : cleanupLogicAppName

resource cleanupScheduler 'Microsoft.Logic/workflows@2019-05-01' = if (deployCleanupScheduler) {
  name: resolvedCleanupLogicAppName
  location: location
  properties: {
    state: 'Enabled'
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      parameters: {
        cleanupAutomationKey: {
          type: 'SecureString'
        }
      }
      triggers: {
        schedule: {
          type: 'Recurrence'
          recurrence: {
            frequency: cleanupScheduleFrequency
            interval: cleanupScheduleInterval
          }
        }
      }
      actions: {
        call_cleanup_endpoint: {
          type: 'Http'
          inputs: {
            method: 'POST'
            uri: cleanupEndpointUrl
            headers: {
              'Content-Type': 'application/json'
              'x-cleanup-key': '@parameters(\'cleanupAutomationKey\')'
            }
            body: {}
          }
          runAfter: {}
          runtimeConfiguration: {
            secureData: {
              properties: [
                'inputs'
                'outputs'
              ]
            }
          }
        }
      }
      outputs: {}
    }
    parameters: {
      cleanupAutomationKey: {
        value: cleanupAutomationKey
      }
    }
  }
}

var emailReceivers = [for receiver in alertEmailReceivers: {
  name: receiver.name
  emailAddress: receiver.email
  useCommonAlertSchema: true
}]

resource alertActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (length(alertEmailReceivers) > 0) {
  name: '${appInsightsName}-email-alerts'
  location: 'Global'
  properties: {
    groupShortName: 'SDVAlert'
    enabled: true
    emailReceivers: emailReceivers
  }
}

var createAlerts = enableScheduledQueryAlerts && length(alertEmailReceivers) > 0

resource unhandledExceptionsAlert 'Microsoft.Insights/scheduledQueryRules@2022-06-15' = if (createAlerts) {
  name: '${appInsightsName}-exceptions'
  location: location
  properties: {
    displayName: 'Unhandled backend exceptions'
    description: 'Alerts when backend exceptions are recorded in Application Insights.'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'exceptions | summarize Count = count()'
          timeAggregation: 'Total'
          metricMeasureColumn: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        alertActionGroup.id
      ]
    }
  }
}

resource http5xxAlert 'Microsoft.Insights/scheduledQueryRules@2022-06-15' = if (createAlerts) {
  name: '${appInsightsName}-http-5xx'
  location: location
  properties: {
    displayName: 'Elevated backend HTTP 5xx responses'
    description: 'Alerts when backend requests return HTTP 5xx responses.'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'requests | where toint(resultCode) >= 500 | summarize Count = count()'
          timeAggregation: 'Total'
          metricMeasureColumn: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        alertActionGroup.id
      ]
    }
  }
}

resource latencyAlert 'Microsoft.Insights/scheduledQueryRules@2022-06-15' = if (createAlerts) {
  name: '${appInsightsName}-request-latency'
  location: location
  properties: {
    displayName: 'Elevated backend request latency'
    description: 'Alerts when average backend request latency is above the pilot threshold.'
    severity: 3
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: 'requests | summarize AverageDurationMs = avg(duration)'
          timeAggregation: 'Average'
          metricMeasureColumn: 'AverageDurationMs'
          operator: 'GreaterThan'
          threshold: 3000
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        alertActionGroup.id
      ]
    }
  }
}

resource tableDependencyAlert 'Microsoft.Insights/scheduledQueryRules@2022-06-15' = if (createAlerts) {
  name: '${appInsightsName}-table-dependency-failures'
  location: location
  properties: {
    displayName: 'Azure Table Storage dependency failures'
    description: 'Alerts when backend telemetry records failed Azure Table dependencies.'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'dependencies | where success == false and (type has "Azure table" or target has "table" or name has "Table") | summarize Count = count()'
          timeAggregation: 'Total'
          metricMeasureColumn: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        alertActionGroup.id
      ]
    }
  }
}

resource smsDependencyAlert 'Microsoft.Insights/scheduledQueryRules@2022-06-15' = if (createAlerts) {
  name: '${appInsightsName}-sms-provider-failures'
  location: location
  properties: {
    displayName: 'SMS provider failures'
    description: 'Alerts when backend telemetry records failed SMS provider dependencies.'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'dependencies | where success == false and (target has "sms" or name has "sms" or target has "SMS-Fly" or name has "SMS-Fly") | summarize Count = count()'
          timeAggregation: 'Total'
          metricMeasureColumn: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        alertActionGroup.id
      ]
    }
  }
}

output staticWebAppName string = staticWebAppName
output storageAccountName string = storageAccount.name
output tableNames array = tableNames
output appInsightsName string = appInsights.name
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
output cleanupLogicAppName string = deployCleanupScheduler ? cleanupScheduler.name : ''
