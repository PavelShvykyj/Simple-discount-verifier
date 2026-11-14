#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('SelfTest','Setup')][string]$Action='SelfTest',
    [string]$SettingsPath,
    [ValidatePattern('\A[a-z0-9][a-z0-9_-]{0,31}\z')][string]$DeploymentEnvironment='develop'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
function Test-PrivateChallenge($Update,[string]$Challenge,[long]$Started,[long]$Now) {
    if($null -eq $Update.message -or $null -ne $Update.PSObject.Properties['edited_message']) { return $false }
    $message=$Update.message
    foreach($name in @('forward_origin','forward_from','forward_date','sender_chat')) { if($null -ne $message.PSObject.Properties[$name]) { return $false } }
    if($message.chat.type -cne 'private' -or $message.from.is_bot -or [long]$message.chat.id -le 0 -or [long]$message.from.id -le 0 -or [long]$message.chat.id -ne [long]$message.from.id) { return $false }
    return $message.text -ceq ('/setup '+$Challenge) -and [long]$message.date -ge $Started -and [long]$message.date -ge ($Now-60) -and [long]$message.date -le ($Now+5)
}
if($Action -eq 'SelfTest') {
    $sample=@{update_id=1;message=@{chat=@{type='private';id=20};from=@{id=20;is_bot=$false};date=100;text='/setup fixture'}} | ConvertTo-Json -Depth 5 | ConvertFrom-Json
    if(-not (Test-PrivateChallenge $sample 'fixture' 99 100) -or (Test-PrivateChallenge $sample 'wrong' 99 100) -or (Test-PrivateChallenge $sample 'fixture' 99 161)) { throw 'Challenge guard self-test failed.' }
    $sample.message.chat.type='group'
    if(Test-PrivateChallenge $sample 'fixture' 99 100) { throw 'Group guard self-test failed.' }
    Write-Output 'PASS private challenge/freshness guards; no network/credentials/files accessed.'
    return
}
. (Join-Path $PSScriptRoot 'Read-ConnectorSettings.ps1')
$Branch=(Read-ConnectorSettings $SettingsPath).Branch
$directory="C:\ProgramData\SimpleDiscountVerifier-AgentExtension\$DeploymentEnvironment\connector\$Branch"
$path=Join-Path $directory 'telegram.credentials.json'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(-not (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Reviewed elevated setup required.' }
if(Test-Path -LiteralPath $path) { throw 'Existing credentials are preserved.' }
for($current=$directory;$current;$current=[IO.Path]::GetDirectoryName($current)) { if((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point refused.' } }
$acl=Get-Acl -LiteralPath $directory
$rules=@($acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]))
if(-not $acl.AreAccessRulesProtected -or $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -cne 'S-1-5-32-544' -or $rules.Count -ne 2) { throw 'Protected directory required.' }
if(@($rules | ForEach-Object { $_.IdentityReference.Value } | Select-Object -Unique).Count -ne 2) { throw 'Distinct SYSTEM/Administrators rules required.' }
foreach($rule in $rules) { if($rule.IsInherited -or $rule.AccessControlType -ne 'Allow' -or $rule.IdentityReference.Value -notin @('S-1-5-18','S-1-5-32-544') -or $rule.FileSystemRights -ne [Security.AccessControl.FileSystemRights]::FullControl) { throw 'Protected directory required.' } }
Add-Type -AssemblyName System.Net.Http
$secure=Read-Host 'Enter NEW bot token; no token is printed or passed in argv' -AsSecureString
$pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
$handler=New-Object Net.Http.HttpClientHandler
$handler.AllowAutoRedirect=$false; $handler.UseProxy=$false; $handler.UseCookies=$false
$client=New-Object Net.Http.HttpClient($handler)
$client.Timeout=[TimeSpan]::FromSeconds(15)
try {
    $token=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if($token -cnotmatch '\A[0-9]+:[A-Za-z0-9_-]{20,}\z') { throw 'Bot credential format invalid.' }
    function Invoke-Bot([string]$Method,$Body) {
        $content=New-Object Net.Http.StringContent(($Body | ConvertTo-Json -Depth 5),[Text.Encoding]::UTF8,'application/json')
        $response=$null
        try {
            $response=$client.PostAsync(('https://api.telegram.org/bot'+$token+'/'+$Method),$content).GetAwaiter().GetResult()
            if(-not $response.IsSuccessStatusCode) { throw 'TelegramRequestFailed' }
            $raw=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if([Text.Encoding]::UTF8.GetByteCount($raw) -gt 262144) { throw 'TelegramResponseTooLarge' }
            $value=$raw | ConvertFrom-Json
            if(-not $value.ok) { throw 'TelegramRequestFailed' }
            return $value.result
        } catch { throw 'Telegram setup request failed; credential/URL/body/exception suppressed.' }
        finally { $content.Dispose(); if($null -ne $response) { $response.Dispose() } }
    }
    $me=Invoke-Bot 'getMe' @{}
    if(-not $me.is_bot -or $me.username -cnotmatch '\A[A-Za-z0-9_]{5,32}\z') { throw 'Unexpected bot identity.' }
    Write-Output ('Open your new bot @'+$me.username+' in a PRIVATE chat.')
    $pending=@(Invoke-Bot 'getUpdates' @{offset=-1;timeout=0;allowed_updates=@('message')})
    $offset=0
    foreach($update in $pending) { if($null -ne $update) { $offset=[Math]::Max($offset,([long]$update.update_id+1)) } }
    $random=New-Object byte[] 16
    $rng=[Security.Cryptography.RandomNumberGenerator]::Create(); try { $rng.GetBytes($random) } finally { $rng.Dispose() }
    $challenge=([BitConverter]::ToString($random)).Replace('-','').ToLowerInvariant()
    $started=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Write-Output ('Send exactly: /setup '+$challenge)
    $accepted=$null
    while([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() -lt ($started+300) -and $null -eq $accepted) {
        $updates=@(Invoke-Bot 'getUpdates' @{offset=$offset;timeout=10;allowed_updates=@('message')})
        foreach($update in $updates) {
            if($null -eq $update -or [long]$update.update_id -lt $offset) { continue }
            $offset=[long]$update.update_id+1
            try { if(Test-PrivateChallenge $update $challenge $started ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())) { $accepted=$update.message; break } } catch { }
        }
    }
    if($null -eq $accepted) { throw 'Private challenge timed out; nothing saved.' }
    if((Read-Host 'Private challenge matched. Authorize only this sender/chat? Type YES') -cne 'YES') { throw 'Setup declined; nothing saved.' }
    $value=@{version=1;branchId=$Branch;botToken=$token;chatId=[long]$accepted.chat.id;allowedUserIds=@([long]$accepted.from.id)}
    $fileAcl=New-Object Security.AccessControl.FileSecurity
    $fileAcl.SetOwner((New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544'))); $fileAcl.SetAccessRuleProtection($true,$false)
    foreach($sid in @('S-1-5-18','S-1-5-32-544')) { $fileAcl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier($sid)),[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.AccessControlType]::Allow))) }
    $stream=New-Object IO.FileStream($path,[IO.FileMode]::CreateNew,[Security.AccessControl.FileSystemRights]::FullControl,[IO.FileShare]::None,4096,[IO.FileOptions]::None,$fileAcl)
    try { $bytes=[Text.Encoding]::UTF8.GetBytes(($value | ConvertTo-Json -Depth 4)); $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    Write-Output 'Created protected credential/allowlist; no bot reply sent. Use its values in the local Telegram section of appsettings.Development.json.'
} finally {
    $token=$null; $value=$null; $accepted=$null; $raw=$null; $client.Dispose(); $handler.Dispose()
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secure.Dispose()
}
