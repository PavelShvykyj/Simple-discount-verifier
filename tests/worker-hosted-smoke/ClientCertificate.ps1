function ConvertTo-SmokeBase64Url([byte[]]$Bytes) {
    [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-SmokeClientCertificate {
    $now = [DateTimeOffset]::UtcNow
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
            'CN=sdv-worker-s5-smoke-caller',
            $rsa,
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1
        )
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
        $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddHours(2))
    } finally { $rsa.Dispose() }
}

function New-SmokeClientAssertion(
    [Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
    [guid]$TenantId,
    [guid]$AppId
) {
    $now = [DateTimeOffset]::UtcNow
    if (!$Certificate.HasPrivateKey -or $TenantId -eq [guid]::Empty -or $AppId -eq [guid]::Empty -or
        $Certificate.NotAfter.ToUniversalTime() -lt $now.AddMinutes(4).UtcDateTime) {
        throw 'The smoke caller certificate or identity is invalid.'
    }
    $head = @{
        alg = 'PS256'; typ = 'JWT'
        'x5t#S256' = ConvertTo-SmokeBase64Url ([Security.Cryptography.SHA256]::HashData($Certificate.RawData))
    } | ConvertTo-Json -Compress
    $claims = @{
        iss = $AppId.ToString('D'); sub = $AppId.ToString('D')
        aud = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token"
        iat = $now.ToUnixTimeSeconds(); nbf = $now.AddSeconds(-30).ToUnixTimeSeconds()
        exp = $now.AddMinutes(4).ToUnixTimeSeconds(); jti = [guid]::NewGuid().ToString('D')
    } | ConvertTo-Json -Compress
    $signingInput = (ConvertTo-SmokeBase64Url ([Text.Encoding]::UTF8.GetBytes($head))) + '.' +
        (ConvertTo-SmokeBase64Url ([Text.Encoding]::UTF8.GetBytes($claims)))
    $key = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
    try {
        $signature = $key.SignData([Text.Encoding]::ASCII.GetBytes($signingInput),
            [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pss)
        $signingInput + '.' + (ConvertTo-SmokeBase64Url $signature)
    } finally { $key.Dispose() }
}
