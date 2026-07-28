param(
    [string]$AssemblyPath = (
        Join-Path $PSScriptRoot `
            "..\bin\Release\net472\SimpleDiscountVerifier.PosHmacCom.dll"
    )
)

$ErrorActionPreference = "Stop"

$resolvedAssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
Add-Type -Path $resolvedAssemblyPath

$component = New-Object `
    SimpleDiscountVerifier.PosHmacCom.PosHmacSha256

$actual = $component.ComputeBase64(
    "The quick brown fox jumps over the lazy dog",
    "key"
)
$expected = "97yD9DBThCSxMpjmqm+xQ+9NWaFJRhdZl0edvC0aPNg="

if ($actual -ne $expected) {
    throw "Unexpected signature. Expected '$expected', actual '$actual'."
}

Write-Host "Managed assembly smoke test passed."
