[CmdletBinding()]
param(
    [ValidateSet("x86", "x64")]
    [string]$Architecture = "x86",

    [string]$AssemblyPath = (
        Join-Path $PSScriptRoot `
            "..\bin\Release\net472\SimpleDiscountVerifier.PosHmacCom.dll"
    )
)

$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal `
    -ArgumentList $identity

if (-not $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )) {
    throw "Run PowerShell as Administrator."
}

$resolvedAssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$frameworkDirectory = if ($Architecture -eq "x86") {
    Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319"
}
else {
    Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
}

$regAsmPath = Join-Path $frameworkDirectory "RegAsm.exe"

if (-not (Test-Path -LiteralPath $regAsmPath)) {
    throw "RegAsm.exe was not found: $regAsmPath"
}

$typeLibraryPath = [IO.Path]::ChangeExtension(
    $resolvedAssemblyPath,
    "$Architecture.tlb"
)

& $regAsmPath `
    $resolvedAssemblyPath `
    /codebase `
    "/tlb:$typeLibraryPath"

if ($LASTEXITCODE -ne 0) {
    throw "RegAsm failed with exit code $LASTEXITCODE."
}

Write-Host "Registered SimpleDiscountVerifier.PosHmacSha256 for $Architecture."
Write-Host "Assembly: $resolvedAssemblyPath"
Write-Host "Type library: $typeLibraryPath"
