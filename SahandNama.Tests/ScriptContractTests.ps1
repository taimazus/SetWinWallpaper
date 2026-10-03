$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$script:passed = 0
function Assert([bool]$condition, [string]$name) {
    if (!$condition) { throw $name }
    $script:passed++
    Write-Output "PASS $name"
}
foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $repo 'SahandNama/Scripts') -Filter '*.ps1') + @(Get-Item -LiteralPath (Join-Path $repo 'Clean-Legacy.ps1'), (Join-Path $repo 'tools/New-AppIcon.ps1'))) {
    $tokens = $null; $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    Assert ($errors.Count -eq 0) "PowerShell parses: $($file.Name)"
}
$workflow = Get-Content -LiteralPath (Join-Path $repo '.github/workflows/build-and-release.yml') -Raw
Assert ($workflow -match '(?m)name: Attach Assets to GitHub Release\r?\n\s+if: startsWith\(github.ref, ''refs/tags/v''\)') 'D07 release publication is gated to version tags'
foreach ($name in @('Update-Now.cmd','Sync-Server-All.cmd','AutoRepair-Spotlight.cmd')) {
    $content = Get-Content -LiteralPath (Join-Path $repo "SahandNama/Scripts/$name") -Raw
    Assert ($content.Contains('SahandNama.exe') -and !$content.Contains('BingWallpaperPro.exe')) "D02 executable contract: $name"
}
# Parse and run the real helper with only SCM/ACL/transcript calls replaced.
# All filesystem writes are confined to a unique test directory; no service is installed.
$root = [IO.Path]::GetFullPath((Join-Path $repo ('artifacts/service-contract-' + [guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $root | Out-Null
$env:ProgramFiles = Join-Path $root 'ProgramFiles'
$env:ProgramData = Join-Path $root 'ProgramData'
$source = Join-Path $root 'source'
New-Item -ItemType Directory -Path (Join-Path $source 'Scripts') -Force | Out-Null
$exe = Join-Path $source 'SahandNama-v1.8.0-win-x64.exe'
Set-Content -LiteralPath $exe -Value 'test executable bytes; never executed'
$script:started = 0; $script:nativeFailure = ''; $script:account = ''
function Start-Transcript { }
function Stop-Transcript { }
function Get-Service { return $null }
function Set-Acl { }
function Start-Service { $script:started++ }
function Invoke-CimMethod { param($ClassName,$MethodName,$Arguments,$InputObject); $script:account=$Arguments.StartName; return @{ ReturnValue = 0 } }
function sc.exe { $global:LASTEXITCODE = if ($args[0] -eq $script:nativeFailure) { 1 } else { 0 } }
$helper = Get-Content -LiteralPath (Join-Path $repo 'SahandNama/Scripts/Manage-Service.ps1') -Raw
$helper = $helper -replace '(?m)^#Requires[^\r\n]*', '' -replace '(?m)^\s*exit 0\s*$', 'return 0' -replace '(?m)^\s*exit 1\s*$', 'return 1'
$result = @(& ([scriptblock]::Create($helper)) -Action Install -SourceRoot $source -SourceExe $exe)
Assert ($result[-1] -eq 0 -and $script:started -eq 1 -and $script:account -eq 'NT AUTHORITY\LocalService') 'D02 actual installer flow registers LocalService before starting'
Assert ((Get-Content -LiteralPath (Join-Path $env:ProgramFiles 'BingWallpaperPro/SahandNama.exe') -Raw).Contains('test executable bytes')) 'D02 versioned standalone executable is copied to canonical service path'
$script:nativeFailure = 'description'; $script:started = 0
$result = @(& ([scriptblock]::Create($helper)) -Action Install -SourceRoot $source -SourceExe $exe)
Assert ($result[-1] -eq 1 -and $script:started -eq 0) 'D02 native SCM configuration failure prevents false success/start'
Write-Output "$script:passed script contract checks passed. SCM, ACL and transcript mutations were mocked."
