function Read-ConnectorSettings([string]$SettingsPath) {
    if(-not $SettingsPath) { $SettingsPath=Join-Path $PSScriptRoot 'appsettings.Development.json' }
    $path=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($SettingsPath)
    $root=[IO.Path]::GetPathRoot($path)
    if($root -notmatch '^[a-z]:\\$' -or [IO.DriveInfo]::new($root).DriveType -eq [IO.DriveType]::Network) { throw 'Settings must be on a local drive.' }
    $file=[IO.Path]::GetFileName($path)
    $legacy=$file -ieq 'connector.config.json'
    if(-not $legacy -and $file -ine 'appsettings.Development.json') { throw 'Select an appsettings.Development.json file.' }
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Settings file not found: $path" }
    for($current=$path;$current;$current=[IO.Path]::GetDirectoryName($current)) {
        if((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point in settings path.' }
    }
    $bytes=[IO.File]::ReadAllBytes($path)
    $reader=[IO.StreamReader]::new([IO.MemoryStream]::new($bytes))
    try { $json=$reader.ReadToEnd() } finally { $reader.Dispose() }
    try { $settings=ConvertFrom-Json -InputObject $json }
    catch { throw "Cannot read JSON settings from '$path'." }
    try { $branch=if($legacy) { $settings.branchId } else { $settings.Connector.BranchId } }
    catch { throw "Set BranchId in the settings file '$path'." }
    if($branch -isnot [string] -or $branch -notmatch '\A[0-9]{1,128}\z') { throw "BranchId in '$path' must be a string of digits." }
    return [pscustomobject]@{Branch=$branch;Bytes=$bytes;Legacy=$legacy}
}
