$ErrorActionPreference = "Stop"

$component = New-Object -ComObject "SimpleDiscountVerifier.PosHmacSha256"
$actual = $component.ComputeBase64(
    "The quick brown fox jumps over the lazy dog",
    "key"
)
$expected = "97yD9DBThCSxMpjmqm+xQ+9NWaFJRhdZl0edvC0aPNg="

if ($actual -ne $expected) {
    throw "Unexpected signature. Expected '$expected', actual '$actual'."
}

Write-Host "COM smoke test passed."
