param([Parameter(Mandatory)][string]$AssemblyPath, [switch]$StateReadGuards)

$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$socket = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$socket.Start()
try { $port = ([Net.IPEndPoint]$socket.LocalEndpoint).Port }
finally { $socket.Stop() }
$endpoint = "http://127.0.0.1:$port"
$session = [guid]::NewGuid().ToString('D')
$nonce = [guid]::NewGuid().ToString('D')
$signer = [Security.Cryptography.RSA]::Create(2048)
$launch = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
$launch.ArgumentList.Add($assembly)
$launch.WorkingDirectory = Split-Path $assembly -Parent
$launch.UseShellExecute = $false
$launch.CreateNoWindow = $true
$launch.Environment['PORT'] = [string]$port
$launch.Environment['ASPNETCORE_URLS'] = $endpoint
$launch.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE'] = 'false'
$launch.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = 'true'
$launch.Environment['FOUNDRY_HOSTING_ENVIRONMENT'] = '1'
$launch.Environment['FOUNDRY_AGENT_NAME'] = 'sdv-worker-s5-smoke'
$version = if ($StateReadGuards) { '3' } else { '1' }
$purpose = if ($StateReadGuards) { 'sdv-s5-state-read' } else { 'sdv-s5-hosted-smoke' }
$launch.Environment['FOUNDRY_AGENT_VERSION'] = $version
$launch.Environment['FOUNDRY_AGENT_SESSION_ID'] = $session
$launch.Environment['SDV_SMOKE_PUBLIC_KEY'] = [Convert]::ToBase64String($signer.ExportSubjectPublicKeyInfo())
[void]$launch.Environment.Remove('SDV_SMOKE_STATE_READ')
if ($StateReadGuards) {
    $launch.Environment['SDV_SMOKE_STATE_READ'] = '1'
    $launch.Environment['SDV_SMOKE_STATE_READ_UNTIL'] = [string][DateTimeOffset]::UtcNow.AddMinutes(3).ToUnixTimeSeconds()
    $launch.Environment['FOUNDRY_PROJECT_ENDPOINT'] = 'https://example.invalid/api/projects/no-network'
}
foreach ($name in @($launch.Environment.Keys)) {
    if ($name -match '^(APPLICATIONINSIGHTS|AZURE_MONITOR|OTEL_EXPORTER_)') {
        [void]$launch.Environment.Remove($name)
    }
}

function Invoke-Probe([int]$Status, [hashtable]$Changes = @{}, [string]$PlatformUser = 'opaque-test-user-a',
    [string]$PlatformCall = 'fixture-call', [string]$Selector = $session, [switch]$Unsigned, [switch]$Tamper,
    [string]$RawJson, [string]$ContentType = 'application/json') {
    $body = [ordered]@{
        purpose = $purpose; probeId = [guid]::NewGuid().ToString('D')
        session = $session; version = $version; delegatedKey = 'sdv:s5:probe:a'
        runNonce = $nonce; expectedFingerprint = $null
        expiresAt = [DateTimeOffset]::UtcNow.AddSeconds(30).ToUnixTimeSeconds()
    }
    foreach ($key in $Changes.Keys) { $body[$key] = $Changes[$key] }
    $json = if ($RawJson) { $RawJson } else { ConvertTo-Json -InputObject $body -Compress }
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $headers = @{ 'x-agent-user-id' = $PlatformUser; 'x-agent-foundry-call-id' = $PlatformCall; 'x-client-role' = 'owner' }
    if (!$Unsigned) {
        $signature = $signer.SignData($bytes, [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pss)
        $headers['x-client-sdv-signature'] = [Convert]::ToBase64String($signature)
    }
    if ($Tamper) { $bytes = [Text.Encoding]::UTF8.GetBytes("$json ") }
    $result = Invoke-WebRequest -Uri "$endpoint/invocations?agent_session_id=$Selector" -Method Post -NoProxy -TimeoutSec 3 `
        -Headers $headers -Body $bytes -ContentType $ContentType -SkipHttpErrorCheck
    if ($result.StatusCode -ne $Status) { throw "Expected HTTP $Status, received $($result.StatusCode)." }
    if ($Status -eq 200) {
        if ($result.Content -match 'opaque-test-user|fixture-call' -or
            [string]$result.Headers['Content-Type'] -notlike 'application/json*') { throw 'Unexpected response content.' }
        $ack = $result.Content | ConvertFrom-Json
        if ($ack.schemaVersion -ne 1 -or $ack.probeId -ne $body.probeId -or $ack.version -ne '1' -or
            !$ack.platformUserPresent -or !$ack.platformCallPresent -or $ack.fingerprint -cnotmatch '^[a-f0-9]{64}$') {
            throw 'Invalid diagnostic ACK.'
        }
        return $ack
    }
    if ($result.Content) { throw 'Unexpected body on refusal.' }
}

$process = [Diagnostics.Process]::Start($launch)
try {
    $ready = $null
    for ($i = 0; $i -lt 30; $i++) {
        if ($process.HasExited) { throw 'Local diagnostic host exited before readiness.' }
        try { $ready = Invoke-WebRequest "$endpoint/readiness" -NoProxy -TimeoutSec 1 -SkipHttpErrorCheck }
        catch { Start-Sleep -Milliseconds 100 }
        if ($ready) { break }
    }
    if (!$ready -or $ready.StatusCode -ne 200) { throw 'Readiness failed.' }
    if ($StateReadGuards) {
        Invoke-Probe 403 -PlatformUser 'sdv:s5:probe:a' -Unsigned
        Invoke-Probe 403 -PlatformUser 'sdv:s5:probe:a' -Tamper
        Invoke-Probe 403 -PlatformUser 'sdv:s5:probe:b'
        Invoke-Probe 403 -PlatformUser 'sdv:s5:probe:a' -Changes @{ purpose = 'sdv-s5-hosted-smoke' }
        Invoke-Probe 403 -PlatformUser 'sdv:s5:probe:a' -Changes @{ version = '2' }
        Invoke-Probe 503 -PlatformUser 'sdv:s5:probe:a'
        'PASS: State Store probe readiness and six HTTP guard checks'
        return
    }
    $a = Invoke-Probe 200
    $repeat = Invoke-Probe 200 -Changes @{ expectedFingerprint = $a.fingerprint }
    $b = Invoke-Probe 200 -PlatformUser 'opaque-test-user-b' -Changes @{ delegatedKey = 'sdv:s5:probe:b' }
    $equal = Invoke-Probe 200 -PlatformUser 'sdv:s5:probe:a'
    if ($a.fingerprint -ne $repeat.fingerprint -or $a.fingerprint -eq $b.fingerprint -or
        $a.matchesDelegatedKey -or !$equal.matchesDelegatedKey) { throw 'Invalid identity comparisons.' }
    Invoke-Probe 403 -PlatformUser 'opaque-test-user-b' -Changes @{ expectedFingerprint = $a.fingerprint }
    Invoke-Probe 403 -PlatformUser 'sdv-smoke-forged-user'
    Invoke-Probe 403 -PlatformCall 'sdv-smoke-forged-call'
    Invoke-Probe 403 -PlatformCall ''
    Invoke-Probe 403 -Unsigned
    Invoke-Probe 403 -Tamper
    Invoke-Probe 403 -Changes @{ expiresAt = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() - 1 }
    Invoke-Probe 403 -Changes @{ expiresAt = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() + 150 }
    Invoke-Probe 403 -Changes @{ version = '2' }
    Invoke-Probe 403 -Selector ([guid]::NewGuid().ToString('D'))
    Invoke-Probe 403 -Selector "$session&agent_session_id=$session"
    Invoke-Probe 403 -RawJson '{"purpose":"x","purpose":"y"}'
    Invoke-Probe 403 -Changes @{ extra = 'unexpected' }
    Invoke-Probe 413 -RawJson ('x' * 2049)
    Invoke-Probe 415 -ContentType 'text/plain'
    'PASS: readiness and 19 local HTTP probes'
}
finally {
    if (!$process.HasExited) { $process.Kill() }
    if (!$process.WaitForExit(5000)) { throw 'The owned local process did not exit.' }
    $process.Dispose()
    $signer.Dispose()
}
