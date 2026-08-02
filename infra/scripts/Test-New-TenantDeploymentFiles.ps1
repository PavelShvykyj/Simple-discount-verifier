Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$tempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase "sdv-tenant-generator-$([guid]::NewGuid().ToString('N'))"

try {
    $templateDirectory = Join-Path $testRoot 'infra\templates'
    [System.IO.Directory]::CreateDirectory($templateDirectory) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\templates\azure-static-web-apps-tenant.yml') -Destination $templateDirectory

    & (Join-Path $PSScriptRoot 'New-TenantDeploymentFiles.ps1') `
        -TenantSlug testtenant `
        -ReleaseBranch release/testtenant `
        -OutputRoot $testRoot `
        -SkipBranchCheck | Out-Null

    $parameters = Get-Content -Raw -Encoding UTF8 (Join-Path $testRoot 'infra\parameters\tenants\testtenant.json') | ConvertFrom-Json
    $workflow = Get-Content -Raw -Encoding UTF8 (Join-Path $testRoot '.github\workflows\azure-static-web-apps-testtenant.yml')

    if ($parameters.parameters.branch.value -cne 'release/testtenant') { throw 'Generated branch is incorrect.' }
    if ($parameters.parameters.storageAccountName.value -cne 'sdvstoragetesttenant') { throw 'Generated storage name is incorrect.' }
    if ($parameters.parameters.deployCleanupScheduler.value) { throw 'Cleanup scheduler must be disabled initially.' }
    if ($workflow -notmatch 'AZURE_STATIC_WEB_APPS_API_TOKEN_TESTTENANT') { throw 'Generated GitHub secret name is incorrect.' }
    if ($workflow -notmatch 'production_branch: "release/testtenant"') { throw 'Generated production branch is incorrect.' }

    $bicep = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '..\main.bicep')
    $bicepParameterNames = [regex]::Matches($bicep, '(?m)^param\s+([A-Za-z0-9_]+)\s') |
        ForEach-Object { $_.Groups[1].Value }
    $unknownParameters = $parameters.parameters.PSObject.Properties.Name |
        Where-Object { $_ -notin $bicepParameterNames }
    if ($unknownParameters) { throw "Generated unknown Bicep parameters: $($unknownParameters -join ', ')." }

    Write-Output 'Tenant deployment file generator check passed.'
}
finally {
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedTestRoot)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
