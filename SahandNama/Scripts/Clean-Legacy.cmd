@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ps = Join-Path '%~dp0' 'Clean-Legacy.ps1'; if (Test-Path $ps) { Start-Process powershell.exe -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File ""' + $ps + '""') -Verb RunAs } else { Write-Host 'File Clean-Legacy.ps1 not found.' -ForegroundColor Red; pause }"
