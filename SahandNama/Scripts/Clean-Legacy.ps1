#Requires -RunAs
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "پاکسازی کامل تسک‌ها و فایل‌های قدیمی Bing Wallpaper"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "[سهند نما] پاکسازی تسک‌ها و فایل‌های قدیمی و اضافی سیستم" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Delete legacy tasks
$legacyTasks = @("Bing Wallpaper Daily", "BingWallpaperDaily", "BingWallpaper_Enterprise")
foreach ($taskName in $legacyTasks) {
    Write-Host "[تسک] در حال بررسی و حذف تسک: '$taskName'..." -NoNewline
    $result = schtasks.exe /Delete /TN $taskName /F 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host " [حذف شد]" -ForegroundColor Green
    } else {
        Write-Host " [یافت نشد یا قبلاً حذف شده]" -ForegroundColor Gray
    }
}

Write-Host ""

# 2. Delete legacy ProgramData folder
$legacyDir = "C:\ProgramData\BingWallpaper"
Write-Host "[پوشه] در حال بررسی و حذف پوشه: '$legacyDir'..." -NoNewline
if (Test-Path $legacyDir) {
    try {
        Remove-Item -Path $legacyDir -Recurse -Force -ErrorAction Stop
        Write-Host " [با موفقیت حذف شد]" -ForegroundColor Green
    } catch {
        Write-Host " [خطا در حذف: $($_.Exception.Message)]" -ForegroundColor Red
    }
} else {
    Write-Host " [پوشه وجود ندارد]" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "[موفقیت] فرآیند پاکسازی تکمیل شد." -ForegroundColor Green
Write-Host "تسک فعال فعلی سیستم: BingWallpaperPro-Daily" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
Read-Host "برای بستن پنجره، کلید Enter را فشار دهید..."
