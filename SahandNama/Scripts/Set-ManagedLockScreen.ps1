#Requires -RunAsAdministrator
param([ValidateSet('Apply','Restore')][string]$Action = 'Apply', [string]$ImagePath)
$ErrorActionPreference = 'Stop'
$policy = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization'
$root = Join-Path $env:ProgramData 'BingWallpaperPro\ManagedLockScreen'
$backupPath = Join-Path $root 'policy-backup.json'
try {
    Start-Transcript -Path (Join-Path $env:TEMP 'BingWallpaperPro-LockScreen-Setup.log') -Append | Out-Null
    $edition = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').EditionID
    if ($edition -notmatch '^(Enterprise|Education|IoTEnterprise|Server)') { throw 'This Group Policy applies only to Enterprise, Education and Server editions.' }
    if ((Get-CimInstance Win32_ComputerSystem).PartOfDomain) { throw 'Domain joined computer: configure lock screen through your domain administrator instead.' }
    $current = (Get-ItemProperty -LiteralPath $policy -Name LockScreenImage -ErrorAction SilentlyContinue).LockScreenImage
    if ($Action -eq 'Restore') {
        if (!(Test-Path -LiteralPath $backupPath)) { throw 'No policy backup exists.' }
        $backup = Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json
        if ($current -ne (Join-Path $root 'lockscreen.jpg')) { throw 'The policy was changed by another administrator; refusing to overwrite it.' }
        if ($backup.HadValue) { Set-ItemProperty -LiteralPath $policy -Name LockScreenImage -Value $backup.Value }
        else { Remove-ItemProperty -LiteralPath $policy -Name LockScreenImage }
    } else {
        if ($current -and $current -ne (Join-Path $root 'lockscreen.jpg')) { throw 'An existing lock screen policy is managed elsewhere. No change was made.' }
        if (!(Test-Path -LiteralPath $ImagePath -PathType Leaf)) { throw 'Image does not exist.' }
        Add-Type -AssemblyName System.Drawing
        $image = [Drawing.Image]::FromFile($ImagePath)
        $image.Dispose()
        $parent = Split-Path $root -Parent
        foreach ($path in @($parent,$root)) {
            if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse points are not allowed in the data path.' }
        }
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $acl = New-Object Security.AccessControl.DirectorySecurity
        $acl.SetAccessRuleProtection($true,$false)
        $acl.SetOwner((New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544')))
        foreach ($entry in @(@('S-1-5-18','FullControl'),@('S-1-5-32-544','FullControl'),@('S-1-5-32-545','ReadAndExecute'))) {
            $sid = New-Object Security.Principal.SecurityIdentifier($entry[0])
            $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($sid,$entry[1],'ContainerInherit,ObjectInherit','None','Allow')))
        }
        Set-Acl -LiteralPath $root -AclObject $acl
        foreach ($path in @($backupPath, (Join-Path $root 'lockscreen.jpg'))) {
            if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse point found in managed files.' }
        }
        if (!(Test-Path -LiteralPath $backupPath)) { @{ HadValue = ($null -ne $current); Value = $current } | ConvertTo-Json | Set-Content -LiteralPath $backupPath -Encoding UTF8 }
        Copy-Item -LiteralPath $ImagePath -Destination (Join-Path $root 'lockscreen.jpg') -Force
        if (!(Test-Path -LiteralPath $policy)) { New-Item -Path $policy -Force | Out-Null }
        Set-ItemProperty -LiteralPath $policy -Name LockScreenImage -Value (Join-Path $root 'lockscreen.jpg')
    }
    # Registry policy delivery may require the next sign-in/policy refresh. Do not claim immediate rendering.
    Stop-Transcript | Out-Null
    exit 0
} catch {
    Write-Output $_.Exception.ToString()
    try { Stop-Transcript | Out-Null } catch {}
    exit 1
}
