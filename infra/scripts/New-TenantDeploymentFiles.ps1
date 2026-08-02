[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z]{3,14}$')]
    [string]$TenantSlug,

    [Parameter(Mandatory)]
    [ValidatePattern('^release/[a-z]{3,14}$')]
    [string]$ReleaseBranch,

    [ValidatePattern('^[a-z0-9]+$')]
    [string]$Location = 'eastus2',

    [string]$ResourceGroupName = 'rg-simple-discount-verifier',

    [ValidatePattern('^https://github\.com/[^/]+/[^/]+/?$')]
    [string]$RepositoryUrl = 'https://github.com/PavelShvykyj/Simple-discount-verifier',

    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\..'),

    [switch]$Force,

    [switch]$SkipBranchCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedBranch = "release/$TenantSlug"
if ($ReleaseBranch -cne $expectedBranch) {
    throw "ReleaseBranch must be '$expectedBranch' for tenant '$TenantSlug'."
}

$root = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $SkipBranchCheck -and (Test-Path -LiteralPath (Join-Path $root '.git'))) {
    $currentBranch = (& git -C $root branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $currentBranch -cne $ReleaseBranch) {
        throw "Run this generator from '$ReleaseBranch'. Current branch: '$currentBranch'."
    }
}

$staticWebAppName = "swa-simple-discount-verifier-$TenantSlug"
$storageAccountName = "sdvstorage$TenantSlug"
$githubSecretName = "AZURE_STATIC_WEB_APPS_API_TOKEN_$($TenantSlug.ToUpperInvariant())"
$parameterPath = Join-Path $root "infra\parameters\tenants\$TenantSlug.json"
$workflowPath = Join-Path $root ".github\workflows\azure-static-web-apps-$TenantSlug.yml"
$templatePath = Join-Path $root 'infra\templates\azure-static-web-apps-tenant.yml'

if (-not (Test-Path -LiteralPath $templatePath)) {
    throw "Workflow template not found: $templatePath"
}

foreach ($path in @($parameterPath, $workflowPath)) {
    if ((Test-Path -LiteralPath $path) -and -not $Force) {
        throw "File already exists: $path. Use -Force only after reviewing the current tenant files."
    }
}

$parameterDocument = [ordered]@{
    '$schema' = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters = [ordered]@{
        environmentName = @{ value = $TenantSlug }
        location = @{ value = $Location }
        staticWebAppName = @{ value = $staticWebAppName }
        manageStaticWebAppResource = @{ value = $false }
        staticWebAppSkuName = @{ value = 'Free' }
        repositoryUrl = @{ value = $RepositoryUrl.TrimEnd('/') }
        branch = @{ value = $ReleaseBranch }
        appLocation = @{ value = './frontend' }
        apiLocation = @{ value = './functions' }
        outputLocation = @{ value = '/dist/app/browser' }
        storageAccountName = @{ value = $storageAccountName }
        storageSkuName = @{ value = 'Standard_LRS' }
        tableNames = @{ value = @('ScannerSurveyResults', 'CustomerProfiles', 'DiscountRuntime', 'AuditEvents') }
        logAnalyticsWorkspaceName = @{ value = 'ws-simple-discount-verifier' }
        appInsightsName = @{ value = 'ins-simple-discount-verifier' }
        appInsightsRetentionInDays = @{ value = 30 }
        appInsightsDailyCapGB = @{ value = '0.1' }
        appInsightsDailyCapWarningThreshold = @{ value = 90 }
        appInsightsStopSendNotificationWhenHitCap = @{ value = $false }
        manageStaticWebAppSettings = @{ value = $false }
        auditEventsRetentionDays = @{ value = 30 }
        scannerSurveyTableName = @{ value = 'ScannerSurveyResults' }
        customerProfilesTableName = @{ value = 'CustomerProfiles' }
        discountRuntimeTableName = @{ value = 'DiscountRuntime' }
        auditEventsTableName = @{ value = 'AuditEvents' }
        smsFlySender = @{ value = '' }
        deployCleanupScheduler = @{ value = $false }
        cleanupLogicAppName = @{ value = "la-sdv-cleanup-$TenantSlug" }
        cleanupEndpointUrl = @{ value = '' }
        cleanupScheduleFrequency = @{ value = 'Day' }
        cleanupScheduleInterval = @{ value = 1 }
        alertEmailReceivers = @{ value = @() }
        enableScheduledQueryAlerts = @{ value = $false }
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($parameterPath)) | Out-Null
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($workflowPath)) | Out-Null
[System.IO.File]::WriteAllText(
    $parameterPath,
    (($parameterDocument | ConvertTo-Json -Depth 20) + [Environment]::NewLine),
    $utf8NoBom
)

$workflow = [System.IO.File]::ReadAllText($templatePath)
$workflow = $workflow.Replace('__TENANT_SLUG__', $TenantSlug)
$workflow = $workflow.Replace('__RELEASE_BRANCH__', $ReleaseBranch)
$workflow = $workflow.Replace('__GITHUB_SECRET_NAME__', $githubSecretName)
[System.IO.File]::WriteAllText($workflowPath, $workflow, $utf8NoBom)

[pscustomobject]@{
    TenantSlug = $TenantSlug
    ReleaseBranch = $ReleaseBranch
    ResourceGroup = $ResourceGroupName
    Location = $Location
    StaticWebApp = $staticWebAppName
    StorageAccount = $storageAccountName
    LogAnalytics = 'ws-simple-discount-verifier'
    ApplicationInsights = 'ins-simple-discount-verifier'
    CleanupLogicApp = "la-sdv-cleanup-$TenantSlug"
    GitHubSecret = $githubSecretName
    ParameterFile = $parameterPath
    WorkflowFile = $workflowPath
}
