#Requires -RunAsAdministrator
param([ValidateSet('Install','Remove')][string]$Action = 'Install', [string]$SourceRoot = (Split-Path $PSScriptRoot -Parent), [string]$SourceExe)
$ErrorActionPreference = 'Stop'
$serviceName = 'BingWallpaperProFeed'
$installRoot = Join-Path $env:ProgramFiles 'BingWallpaperPro'
$feedRoot = Join-Path $env:ProgramData 'BingWallpaperPro\Feed'
$sourceRoot = [IO.Path]::GetFullPath($SourceRoot)
if (!$SourceExe) { $SourceExe = Join-Path $sourceRoot 'SahandNama.exe' }
if ($Action -eq 'Install' -and (!(Test-Path -LiteralPath $SourceExe -PathType Leaf) -or [IO.Path]::GetFileName($SourceExe) -eq 'dotnet.exe')) { throw 'Publish an apphost or standalone executable before installing the service.' }
try {
    Start-Transcript -Path (Join-Path $env:TEMP 'BingWallpaperPro-Service-Setup.log') -Append | Out-Null
    $existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existing) {
        Stop-Service -Name $serviceName -Force
        $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    if ($Action -eq 'Remove') {
        if ($existing) {
            & sc.exe delete $serviceName
            if ($LASTEXITCODE -ne 0) { throw 'Service deletion failed.' }
        }
        # Keep binaries and cached pictures; never recursively delete user data.
    } else {
        New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
        if ((Get-Item -LiteralPath $installRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Installation path cannot be a reparse point.' }
        if ([IO.Path]::GetFullPath($sourceRoot) -ne [IO.Path]::GetFullPath($installRoot)) {
            Get-ChildItem -LiteralPath $sourceRoot -File | Where-Object { $_.Extension -in '.exe','.dll','.json','.pdb' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $installRoot -Force }
            $scriptsRoot = Join-Path $installRoot 'Scripts'
            New-Item -ItemType Directory -Path $scriptsRoot -Force | Out-Null
            Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Scripts') -Filter '*.ps1' -File -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $scriptsRoot -Force }
        }
        $targetExe = Join-Path $installRoot 'SahandNama.exe'
        if ([IO.Path]::GetFullPath($SourceExe) -ne [IO.Path]::GetFullPath($targetExe)) { Copy-Item -LiteralPath $SourceExe -Destination $targetExe -Force }
        foreach ($directory in @((Split-Path $feedRoot -Parent), $feedRoot)) {
            if ((Test-Path -LiteralPath $directory) -and ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Data path cannot be a reparse point.' }
        }
        New-Item -ItemType Directory -Path $feedRoot -Force | Out-Null
        $acl = New-Object Security.AccessControl.DirectorySecurity
        $acl.SetAccessRuleProtection($true,$false)
        foreach ($entry in @(@('S-1-5-18','FullControl'),@('S-1-5-32-544','FullControl'),@('S-1-5-19','Modify'),@('S-1-5-32-545','ReadAndExecute'))) {
            $sid = New-Object Security.Principal.SecurityIdentifier($entry[0])
            $rule = New-Object Security.AccessControl.FileSystemAccessRule($sid,$entry[1],'ContainerInherit,ObjectInherit','None','Allow')
            $acl.AddAccessRule($rule)
        }
        Set-Acl -LiteralPath $feedRoot -AclObject $acl
        $binary = '"' + (Join-Path $installRoot 'SahandNama.exe') + '" --service'
        if ($existing) {
            $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
            $result = Invoke-CimMethod -InputObject $service -MethodName Change -Arguments @{ PathName=$binary; StartMode='Automatic'; StartName='NT AUTHORITY\LocalService'; StartPassword='' }
        } else {
            $result = Invoke-CimMethod -ClassName Win32_Service -MethodName Create -Arguments @{ Name=$serviceName; DisplayName='Bing Wallpaper Pro - Download Feed'; PathName=$binary; ServiceType=[uint32]16; ErrorControl=[uint32]1; StartMode='Automatic'; DesktopInteract=$false; StartName='NT AUTHORITY\LocalService' }
        }
        if ($result.ReturnValue -ne 0) { throw "Service registration failed: $($result.ReturnValue)" }
        & sc.exe config $serviceName start= delayed-auto
        if ($LASTEXITCODE -ne 0) { throw 'Could not configure delayed startup.' }
        & sc.exe description $serviceName 'Downloads online image catalogs to the shared feed; retries failed sync hourly. Desktop changes run in the interactive user scheduled task.'
        if ($LASTEXITCODE -ne 0) { throw 'Could not configure service description.' }
        & sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/300000/restart/900000
        if ($LASTEXITCODE -ne 0) { throw 'Could not configure service recovery.' }
        Start-Service -Name $serviceName
    }
    Stop-Transcript | Out-Null
    exit 0
} catch {
    Write-Output $_.Exception.ToString()
    try { Stop-Transcript | Out-Null } catch {}
    exit 1
}
