#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('SelfTest','Prepare','Install','Validate','Start','Stop','Update','Remove')][string]$Action='Validate',
    [string]$PublishDirectory,
    [string]$ExpectedArtifactHash,
    [string]$ReleaseId,
    [string]$SettingsPath,
    [ValidatePattern('\A[a-z0-9][a-z0-9_-]{0,31}\z')][string]$DeploymentEnvironment='develop'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($Action -eq 'SelfTest') {
    $Branch='203'
} else {
    . (Join-Path $PSScriptRoot 'Read-ConnectorSettings.ps1')
    $localSettings=Read-ConnectorSettings $SettingsPath
    $Branch=$localSettings.Branch
}
$data="C:\ProgramData\SimpleDiscountVerifier-AgentExtension\$DeploymentEnvironment\connector"
$bin="C:\Program Files\SimpleDiscountVerifier-AgentExtension\$DeploymentEnvironment\connector"
$directory=Join-Path $data $Branch
$legacyConfig=Join-Path $directory 'connector.config.json'
$developmentFile='appsettings.Development.json'
$developmentConfig=Join-Path $directory $developmentFile
$record=Join-Path $directory 'installation.json'
$name="SDV-AgentExtension-$DeploymentEnvironment-Connector-$Branch"
$serviceRegistry='HKLM:\SYSTEM\CurrentControlSet\Services\'+$name
$serviceEnvironment=@{
    DOTNET_ENVIRONMENT='Development'
    DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE='false'
    Connector__Environment=$DeploymentEnvironment
    Connector__ServiceName=$name
}
$admins=New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544')
$system=New-Object Security.Principal.SecurityIdentifier('S-1-5-18')
function Assert-Path([string]$Path,[string]$Root) {
    $resolved=[IO.Path]::GetFullPath($Path)
    if(-not $resolved.StartsWith($Root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside approved root.' }
    for($current=$resolved;$current;$current=[IO.Path]::GetDirectoryName($current)) {
        if((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse point refused.' }
    }
    return $resolved
}
function New-ProtectedAcl([bool]$IsDirectory) {
    if($IsDirectory) { $acl=New-Object Security.AccessControl.DirectorySecurity } else { $acl=New-Object Security.AccessControl.FileSecurity }
    $acl.SetOwner($admins); $acl.SetAccessRuleProtection($true,$false)
    foreach($sid in @($admins,$system)) {
        $rule=New-Object Security.AccessControl.FileSystemAccessRule($sid,[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    return $acl
}
function Protect-New([string]$Path,[bool]$IsDirectory) {
    Set-Acl -LiteralPath $Path -AclObject (New-ProtectedAcl $IsDirectory)
}
function Test-RecoveryPolicy($Properties) {
    # Missing FailureActions is the normal new-service state; do not invoke a missing-value getter.
    return $null -ne $Properties.PSObject.Properties['FailureActions']
}
function Write-ProtectedNew([string]$Path,[byte[]]$Bytes) {
    $stream=New-Object IO.FileStream($Path,[IO.FileMode]::CreateNew,[Security.AccessControl.FileSystemRights]::FullControl,[IO.FileShare]::None,4096,[IO.FileOptions]::None,(New-ProtectedAcl $false))
    try { $stream.Write($Bytes,0,$Bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
function Replace-RecordFile([string]$Source,[string]$Destination) {
    # WinPS5.1 coerces plain $null to an empty string for this string overload.
    [IO.File]::Replace($Source,$Destination,[System.Management.Automation.Language.NullString]::Value)
}
function Get-ReleaseConfig($Files) {
    if($Files.path -ccontains 'appsettings.json') { return $developmentConfig }
    return $legacyConfig
}
function Get-ServiceCommand($Owned) {
    if($Owned.version -eq 1) { return '"{0}" --config "{1}"' -f $Owned.executable,$Owned.config }
    return '"{0}"' -f $Owned.executable
}
function Assert-ServiceEnvironment {
    $property=(Get-ItemProperty -LiteralPath $serviceRegistry).PSObject.Properties['Environment']
    if($null -eq $property -or $property.Value -isnot [string[]]) { throw 'Missing service environment.' }
    foreach($key in $serviceEnvironment.Keys) {
        $entries=@($property.Value | Where-Object { ($_ -split '=',2)[0] -ieq $key })
        if($entries.Count -ne 1 -or $entries[0] -cne "$key=$($serviceEnvironment[$key])") { throw 'Service environment mismatch.' }
    }
}
function Set-ServiceEnvironment {
    $property=(Get-ItemProperty -LiteralPath $serviceRegistry).PSObject.Properties['Environment']
    $entries=@()
    if($null -ne $property) {
        if($property.Value -isnot [string[]]) { throw 'Unexpected service environment format.' }
        $entries=@($property.Value | Where-Object { -not $serviceEnvironment.ContainsKey(($_ -split '=',2)[0]) })
    }
    foreach($key in $serviceEnvironment.Keys) { $entries+="$key=$($serviceEnvironment[$key])" }
    New-ItemProperty -LiteralPath $serviceRegistry -Name Environment -PropertyType MultiString -Value ([string[]]$entries) -Force | Out-Null
    Assert-ServiceEnvironment
}
function Assert-StartedIdentity($Service,$Process,$Owned) {
    $expected=Get-ServiceCommand $Owned
    if($null -eq $Service -or $null -eq $Process -or $Service.Name -cne $name -or $Service.PathName -cne $expected -or $Service.StartName -cne 'LocalSystem' -or $Service.StartMode -cne 'Manual' -or $Service.ProcessId -eq 0 -or $Service.ProcessId -ne $Process.ProcessId -or $Process.ExecutablePath -cne $Owned.executable -or $Process.CommandLine -cne $expected) { throw 'Started service/process identity mismatch.' }
}
function Assert-Protected([string]$Path) {
    $acl=Get-Acl -LiteralPath $Path
    $rules=@($acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]))
    if(-not $acl.AreAccessRulesProtected -or $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -cne $admins.Value -or $rules.Count -ne 2) { throw 'Protected ACL mismatch.' }
    foreach($rule in $rules) { if($rule.IsInherited -or $rule.AccessControlType -ne 'Allow' -or $rule.IdentityReference.Value -notin @($admins.Value,$system.Value) -or $rule.FileSystemRights -ne [Security.AccessControl.FileSystemRights]::FullControl) { throw 'Protected ACE mismatch.' } }
    if(@($rules | Select-Object -ExpandProperty IdentityReference -Unique).Count -ne 2) { throw 'Duplicate protected ACE.' }
}
function Ensure-Directory([string]$Path) {
    if(Test-Path -LiteralPath $Path) {
        if($Path.StartsWith($data,[StringComparison]::OrdinalIgnoreCase) -or $Path.StartsWith($bin,[StringComparison]::OrdinalIgnoreCase)) { Assert-Protected $Path }
        else {
            # Existing shared ancestors are inspected, never re-ACL'd; creation below is atomic with a protected DACL.
            $acl=Get-Acl -LiteralPath $Path
            $write=[Security.AccessControl.FileSystemRights]::WriteData -bor [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor [Security.AccessControl.FileSystemRights]::Delete -bor [Security.AccessControl.FileSystemRights]::ChangePermissions -bor [Security.AccessControl.FileSystemRights]::TakeOwnership
            foreach($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
                if($rule.AccessControlType -eq 'Allow' -and ($rule.FileSystemRights -band $write) -ne 0 -and $rule.IdentityReference.Value -notin @($admins.Value,$system.Value,'S-1-3-0','S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')) { throw 'Shared ancestor permits untrusted writes.' }
            }
        }
        return
    }
    $parent=[IO.Path]::GetDirectoryName($Path)
    Ensure-Directory $parent
    (New-Object IO.DirectoryInfo($Path)).Create((New-ProtectedAcl $true))
}
function Save-Record($Value) {
    $bytes=(New-Object Text.UTF8Encoding($false)).GetBytes(($Value | ConvertTo-Json -Depth 8))
    if(-not (Test-Path -LiteralPath $record)) { Write-ProtectedNew $record $bytes; return }
    Assert-Protected $record
    $replacement=Join-Path $directory ('installation-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    Write-ProtectedNew $replacement $bytes
    try { Replace-RecordFile $replacement $record; Assert-Protected $record }
    finally { if(Test-Path -LiteralPath $replacement) { Remove-Item -LiteralPath $replacement } }
}
function Assert-Owned {
    if(-not (Test-Path -LiteralPath $record)) { throw 'No owned installation record; takeover refused.' }
    Assert-Protected $record
    $owned=Get-Content -Raw -LiteralPath $record | ConvertFrom-Json
    if($owned.version -notin @(1,2) -or $owned.branchId -cne $Branch -or $owned.serviceName -cne $name) { throw 'Owned record context mismatch.' }
    $exe=Assert-Path $owned.executable (Join-Path $bin $Branch)
    $releaseDirectory=[IO.Path]::GetDirectoryName($exe)
    $config=if($owned.version -eq 2) { Join-Path $releaseDirectory $developmentFile } else { Get-ReleaseConfig $owned.files }
    if($owned.config -cne $config) { throw 'Owned config path mismatch.' }
    Assert-Protected $exe
    if($config -ceq $legacyConfig) {
        Assert-Protected $config
        if((Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash -cne $owned.configHash) { throw 'Owned config hash mismatch.' }
    }
    if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -cne $owned.executableHash) { throw 'Owned executable hash mismatch.' }
    foreach($file in $owned.files) {
        $artifact=Assert-Path (Join-Path $releaseDirectory $file.path) $releaseDirectory
        Assert-Protected $artifact
        if((Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash -cne $file.sha256) { throw 'Owned release hash mismatch.' }
    }
    $expected=Get-ServiceCommand $owned
    $service=Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name)
    if($null -eq $service -or $service.PathName -cne $expected -or $service.StartName -cne 'LocalSystem' -or $service.StartMode -cne 'Manual') { throw 'SCM identity mismatch; no mutation made.' }
    $properties=Get-ItemProperty -LiteralPath $serviceRegistry
    if(Test-RecoveryPolicy $properties) { throw 'Unexpected SCM recovery configuration; manual review required.' }
    if($service.ProcessId -ne 0) {
        $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$service.ProcessId)
        if($null -eq $process -or $process.ExecutablePath -cne $exe -or $process.CommandLine -cne $expected) { throw 'Service process identity mismatch.' }
        if($owned.processId -ne $service.ProcessId -or $owned.startTimeUtc -cne $process.CreationDate.ToUniversalTime().ToString('o')) { throw 'Service process start time mismatch.' }
    }
    return $owned
}
function Stage-Release {
    if($ReleaseId -cnotmatch '\A[a-z0-9][a-z0-9-]{0,63}\z' -or $ExpectedArtifactHash -cnotmatch '\A[0-9A-F]{64}\z') { throw 'Explicit release ID and reviewed manifest SHA256 required.' }
    $source=[IO.Path]::GetFullPath($PublishDirectory)
    $manifestPath=Join-Path $source 'artifact-manifest.json'
    if((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -cne $ExpectedArtifactHash) { throw 'Artifact manifest hash mismatch.' }
    $artifact=Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    if($artifact.version -notin @(1,2) -or $artifact.runtime -cne 'win-x64' -or -not $artifact.frameworkDependent) { throw 'Wrong artifact.' }
    $destination=Assert-Path (Join-Path (Join-Path $bin $Branch) $ReleaseId) $bin
    if(Test-Path -LiteralPath $destination) { throw 'Release already exists; never overwrite binaries.' }
    $paths=@{}
    foreach($file in $artifact.files) {
        if([IO.Path]::IsPathRooted($file.path) -or $file.path -match '(^|[\\/])\.\.([\\/]|$)' -or $paths.ContainsKey($file.path)) { throw 'Invalid artifact relative path.' }
        if([IO.Path]::GetFileName($file.path) -ieq $developmentFile) { throw 'Local settings cannot be part of the release.' }
        $paths[$file.path]=$true
        $from=Assert-Path (Join-Path $source $file.path) $source
        if((Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash -cne $file.sha256) { throw 'Source artifact mismatch.' }
    }
    if(-not $paths.ContainsKey('Connector.exe') -or -not $paths.ContainsKey('Connector.dll')) { throw 'Missing application artifacts.' }
    if($artifact.version -eq 2) {
        if(-not $paths.ContainsKey('appsettings.json')) { throw 'Missing appsettings template.' }
        if($localSettings.Legacy) { throw 'Select appsettings.Development.json to install this release.' }
        $config=Join-Path $destination $developmentFile
    } else {
        $config=Get-ReleaseConfig $artifact.files
        Assert-Path $config $directory | Out-Null
        Assert-Protected $config
    }
    Ensure-Directory (Join-Path $bin $Branch); Ensure-Directory $destination
    foreach($file in $artifact.files) {
        $to=Assert-Path (Join-Path $destination $file.path) $destination
        Ensure-Directory ([IO.Path]::GetDirectoryName($to))
        Write-ProtectedNew $to ([IO.File]::ReadAllBytes((Join-Path $source $file.path)))
        if((Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash -cne $file.sha256) { throw 'Protected artifact mismatch; service not changed.' }
    }
    $exe=Join-Path $destination 'Connector.exe'
    if($artifact.version -eq 2) {
        Write-ProtectedNew $config $localSettings.Bytes
    }
    return [pscustomobject]@{version=$artifact.version;executable=$exe;files=$artifact.files;config=$config}
}
if($Action -eq 'SelfTest') {
    $rejected=0
    foreach($bad in @('C:\Windows\System32\cmd.exe',($bin+'\..\foreign\x'),($bin+'-other\x'))) { try { Assert-Path $bad $bin | Out-Null } catch { $rejected++ } }
    if($rejected -ne 3) { throw 'Path guard self-test failed.' }
    if((Test-RecoveryPolicy ([pscustomobject]@{PSPath='synthetic-service'})) -or -not (Test-RecoveryPolicy ([pscustomobject]@{FailureActions=[byte[]]@(0,1)})) -or -not (Test-RecoveryPolicy ([pscustomobject]@{FailureActions=$null}))) { throw 'Recovery property decision self-test failed.' }
    if($ReleaseId -and $ReleaseId -cnotmatch '\A[a-z0-9][a-z0-9-]{0,63}\z') { throw 'Release guard rejected unsafe ID.' }
    $fixture=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\agent-extension\.local\s4-replace-'+[Guid]::NewGuid().ToString('N'))))
    $fixtureRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\agent-extension\.local'))
    if(-not $fixture.StartsWith($fixtureRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Replacement fixture path refused.' }
    New-Item -ItemType Directory -Path $fixture | Out-Null
    $source=Join-Path $fixture 'new.json'; $destination=Join-Path $fixture 'record.json'
    try {
        [IO.File]::WriteAllText($source,'new'); [IO.File]::WriteAllText($destination,'old')
        Replace-RecordFile $source $destination
        if([IO.File]::ReadAllText($destination) -cne 'new' -or (Test-Path -LiteralPath $source) -or @(Get-ChildItem -LiteralPath $fixture -Force).Count -ne 1) { throw 'Real replacement self-test failed.' }
    } finally {
        foreach($file in @($source,$destination)) { if(Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
        Remove-Item -LiteralPath $fixture
    }
    if((Get-ReleaseConfig @(@{path='Connector.exe'})) -cne $legacyConfig -or (Get-ReleaseConfig @(@{path='Connector.exe'},@{path='appsettings.json'})) -cne $developmentConfig) { throw 'Release config selection self-test failed.' }
    $syntheticOwned=[pscustomobject]@{version=2;executable='C:\synthetic\Connector.exe';config=$developmentConfig}
    $syntheticCommand='"C:\synthetic\Connector.exe"'
    $syntheticService=[pscustomobject]@{Name=$name;PathName=$syntheticCommand;StartName='LocalSystem';StartMode='Manual';ProcessId=123}
    $syntheticProcess=[pscustomobject]@{ProcessId=123;ExecutablePath=$syntheticOwned.executable;CommandLine=$syntheticCommand}
    Assert-StartedIdentity $syntheticService $syntheticProcess $syntheticOwned
    $syntheticOwned.version=1
    $syntheticService.PathName=$syntheticCommand+(' --config "{0}"' -f $developmentConfig)
    $syntheticProcess.CommandLine=$syntheticService.PathName
    Assert-StartedIdentity $syntheticService $syntheticProcess $syntheticOwned
    $syntheticProcess.ProcessId=124; $refused=$false
    try { Assert-StartedIdentity $syntheticService $syntheticProcess $syntheticOwned } catch { $refused=$true }
    if(-not $refused) { throw 'Started process mismatch self-test failed.' }
    Write-Output 'PASS lifecycle path/recovery/started-process guards and real disposable File.Replace; no protected files/services/registry changed. SCM cleanup remains host verification.'
    return
}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(-not (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Reviewed elevated execution required.' }
Assert-Path $directory $data | Out-Null
if($Action -eq 'Prepare') {
    if($localSettings.Legacy) { throw 'Select appsettings.Development.json for Prepare.' }
    if(Test-Path -LiteralPath $developmentConfig) { throw 'Existing appsettings.Development.json preserved; review separately.' }
    Ensure-Directory $data; Ensure-Directory $directory
    $stagedSettings=Join-Path $directory ('appsettings-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try {
        Write-ProtectedNew $stagedSettings $localSettings.Bytes
        [IO.File]::Move($stagedSettings,$developmentConfig)
    } finally {
        if(Test-Path -LiteralPath $stagedSettings) { Remove-Item -LiteralPath $stagedSettings }
    }
    Write-Output 'Prepared branch appsettings.Development.json.'
    return
}
$owned=$null
if($Action -eq 'Install') {
    if((Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name)) -or (Test-Path -LiteralPath $record)) { throw 'Service/record exists; takeover refused.' }
} else { $owned=Assert-Owned }
if($Action -eq 'Validate') {
    Assert-Protected $owned.config
    if($owned.version -eq 2) { Assert-ServiceEnvironment }
    Write-Output 'Owned SCM/executable/config/process identity validated; no network check performed.'
    return
}
$staged=$null
if($Action -eq 'Update') { $staged=Stage-Release }
if($Action -in @('Stop','Update','Remove')) {
    Stop-Service -Name $name
    (Get-Service -Name $name).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped,[TimeSpan]::FromSeconds(40))
    $owned.processId=0; $owned.startTimeUtc=$null; Save-Record $owned
}
if($Action -in @('Install','Update')) {
    if($Action -eq 'Install') { $staged=Stage-Release }
    $exe=$staged.executable
    $config=$staged.config
    $configHash=if($config -ceq $legacyConfig) { (Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash } else { $null }
    $quoted=Get-ServiceCommand $staged
    $newRecord=@{version=$staged.version;branchId=$Branch;serviceName=$name;config=$config;configHash=$configHash;executable=$exe;executableHash=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;files=$staged.files;processId=0;startTimeUtc=$null}
    if($Action -eq 'Install') {
        New-Service -Name $name -BinaryPathName $quoted -StartupType Manual | Out-Null
        # A new Manual service has no recovery actions. Refuse an unexpected policy rather than rewriting it.
        $properties=Get-ItemProperty -LiteralPath $serviceRegistry
        if(Test-RecoveryPolicy $properties) { throw 'Unexpected SCM recovery configuration; service remains stopped.' }
        if($staged.version -eq 2) { Set-ServiceEnvironment }
    } else {
        Assert-Owned | Out-Null
        $service=Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name)
        if($service.State -cne 'Stopped' -or $service.ProcessId -ne 0) { throw 'SCM update requires Stopped/PID0.' }
        if($staged.version -eq 2) { Set-ServiceEnvironment }
        # CIM passes the quoted command line without Windows PowerShell native argument splitting.
        $changed=Invoke-CimMethod -InputObject $service -MethodName Change -Arguments @{PathName=$quoted}
        if($changed.ReturnValue -ne 0) { throw 'SCM update failed.' }
    }
    $service=Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name)
    if($null -eq $service -or $service.PathName -cne $quoted -or $service.State -cne 'Stopped' -or $service.ProcessId -ne 0 -or $service.StartName -cne 'LocalSystem' -or $service.StartMode -cne 'Manual') { throw 'SCM release readback mismatch; inspect the stopped registration.' }
    Save-Record $newRecord
    Assert-Owned | Out-Null
    Write-Output 'Installed protected release; service remains Stopped/Manual. Start is a separate action.'
}
if($Action -eq 'Start') {
    Assert-Protected $owned.config
    if($owned.version -eq 2) { Assert-ServiceEnvironment }
    $before=Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name)
    if($before.State -ceq 'Running') { Write-Output 'Already Running; no Start or metadata write performed.'; return }
    if($before.State -cne 'Stopped' -or $before.ProcessId -ne 0) { throw 'Start requires verified Stopped/PID0 service.' }
    $captured=$null
    try {
        Start-Service -Name $name
        $service=Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name)
        $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$service.ProcessId)
        Assert-StartedIdentity $service $process $owned
        $captured=@{processId=$process.ProcessId;startTimeUtc=$process.CreationDate.ToUniversalTime().ToString('o')}
        (Get-Service -Name $name).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Running,[TimeSpan]::FromSeconds(20))
        $owned.processId=$captured.processId; $owned.startTimeUtc=$captured.startTimeUtc; Save-Record $owned
        Assert-Owned | Out-Null
    } catch {
        if($null -eq $captured) { throw 'Start failed before process ownership capture; no automatic process stop attempted. Inspect exact SCM/process identity.' }
        try {
            $service=Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name)
            $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$captured.processId)
            Assert-StartedIdentity $service $process $owned
            if($process.CreationDate.ToUniversalTime().ToString('o') -cne $captured.startTimeUtc -or (Get-FileHash -LiteralPath $owned.executable -Algorithm SHA256).Hash -cne $owned.executableHash) { throw 'Captured identity/hash mismatch.' }
            if($owned.config -ceq $legacyConfig -and (Get-FileHash -LiteralPath $owned.config -Algorithm SHA256).Hash -cne $owned.configHash) { throw 'Captured legacy config hash mismatch.' }
            $handle=Get-Process -Id $captured.processId
            if($handle.StartTime.ToUniversalTime().ToString('o') -cne $captured.startTimeUtc) { throw 'Captured process reused.' }
            Stop-Process -InputObject $handle -ErrorAction Stop
            if(-not $handle.WaitForExit(40000)) { throw 'Captured process did not exit.' }
            (Get-Service -Name $name).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped,[TimeSpan]::FromSeconds(40))
        } catch { throw 'Start verification/record write failed; captured-process cleanup not confirmed. No unverified process was stopped; inspect ownership.' }
        throw 'Start verification/record write failed; only the verified process started by this action was stopped. Ownership record may be stale; service is Stopped.'
    }
    Write-Output 'Owned service Running; this alone does not prove API/Service Bus health.'
}
if($Action -eq 'Remove') {
    & sc.exe delete $name | Out-Null
    if($LASTEXITCODE -ne 0 -or (Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $name))) { throw 'SCM removal incomplete.' }
    # Only our exact metadata is removed; binaries/credentials/logs/evidence are retained.
    Remove-Item -LiteralPath $record
    Write-Output 'Removed owned SCM registration; releases/config/credentials/logs preserved.'
}
