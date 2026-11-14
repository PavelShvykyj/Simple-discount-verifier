#Requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$root=Join-Path $repo 'agent-extension\.local'
$directory=Join-Path $root ('s4-guard-'+[Guid]::NewGuid().ToString('N'))
$resolved=[IO.Path]::GetFullPath($directory)
if(-not $resolved.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture path refused.' }
New-Item -ItemType Directory -Path $directory | Out-Null
$marker=Join-Path $directory 'preserve.txt'
[IO.File]::WriteAllText($marker,'synthetic evidence marker')
$expected=(Get-FileHash -LiteralPath $marker -Algorithm SHA256).Hash
try {
    $publish=Join-Path $repo 'connector\Publish-Connector.ps1'
    $ErrorActionPreference='Continue'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $publish -OutputDirectory $directory *> (Join-Path $directory 'publish-refusal.txt')
    $exit=$LASTEXITCODE; $ErrorActionPreference='Stop'
    if($exit -eq 0 -or (Get-FileHash -LiteralPath $marker -Algorithm SHA256).Hash -cne $expected) { throw 'Publish preservation guard failed.' }
    $client=Join-Path $PSScriptRoot 'bin\Debug\net10.0\ConnectorClient.dll'
    if(-not (Test-Path -LiteralPath $client)) { throw 'Build independent client first.' }
    $env:TEMP=Join-Path $repo 'agent-extension\.local\bmad-temp'; $env:TMP=$env:TEMP
    $env:DOTNET_CLI_HOME=Join-Path $repo 'agent-extension\.local\s4-build'; $env:NUGET_PACKAGES=Join-Path $env:DOTNET_CLI_HOME 'nuget'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
    # Nonempty evidence must reject before AzureCliCredential/client construction; no SB operation is attempted.
    $ErrorActionPreference='Continue'
    & dotnet $client --live-exclusive-window --evidence $directory --approved-exchange *> (Join-Path $directory 'client-refusal.txt')
    $exit=$LASTEXITCODE; $ErrorActionPreference='Stop'
    if($exit -eq 0 -or (Get-FileHash -LiteralPath $marker -Algorithm SHA256).Hash -cne $expected) { throw 'Client preservation guard failed.' }
    if(@(Get-ChildItem -LiteralPath $directory -File).Count -ne 3) { throw 'Unexpected fixture writes.' }
    Write-Output 'PASS publish/client nonempty-evidence refusal; original marker unchanged; no network/services/credentials accessed.'
} finally {
    # Exact known fixture files only; unexpected entries are preserved and make cleanup fail.
    foreach($name in @('preserve.txt','publish-refusal.txt','client-refusal.txt')) {
        $file=Join-Path $directory $name
        if(Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $directory
}
