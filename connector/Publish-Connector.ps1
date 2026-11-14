#Requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
$allowed=Join-Path $repo 'agent-extension\.local\'
if(-not $output.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Publish output must be under agent-extension/.local.' }
if((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count -ne 0) { throw 'Output is not empty; evidence is preserved.' }
$templatePath=Join-Path $PSScriptRoot 'appsettings.json'
try {
    $json=(Get-Content -LiteralPath $templatePath -Encoding UTF8 | Where-Object { $_ -notmatch '^\s*//' }) -join "`n"
    $settings=(ConvertFrom-Json -InputObject $json).Connector
} catch { throw 'Cannot read the public appsettings.json template.' }
if($settings.Api.User -cne '' -or $settings.Api.Password -cne '' -or $settings.ServiceBus.CommandListenConnectionString -cne '' -or
   $settings.ServiceBus.ResultSendConnectionString -cne '' -or $settings.Telegram.BotToken -cne '' -or
   $settings.Telegram.ChatId -ne 0 -or @($settings.Telegram.AllowedUserIds).Count -ne 0) {
    throw 'The public appsettings.json must contain empty credential and Telegram identity placeholders.'
}
$temp=Join-Path $repo 'agent-extension\.local\bmad-temp'
New-Item -ItemType Directory -Path $temp -Force | Out-Null
$env:TEMP=$temp; $env:TMP=$temp
$env:DOTNET_CLI_HOME=Join-Path $repo 'agent-extension\.local\s4-build'
$env:NUGET_PACKAGES=Join-Path $env:DOTNET_CLI_HOME 'nuget'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
& dotnet publish (Join-Path $PSScriptRoot 'Connector.csproj') -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -p:PublishTrimmed=false -o $output
if($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$files=@(Get-ChildItem -LiteralPath $output -File -Recurse | Sort-Object FullName)
if($files.Name -icontains 'appsettings.Development.json') { throw 'Local settings cannot be part of the release.' }
$entries=@($files | ForEach-Object { @{path=$_.FullName.Substring($output.TrimEnd('\').Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
$manifest=Join-Path $output 'artifact-manifest.json'
[IO.File]::WriteAllText($manifest,(@{version=2;runtime='win-x64';frameworkDependent=$true;files=$entries} | ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
Write-Output ('ArtifactManifestSHA256='+(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash)
