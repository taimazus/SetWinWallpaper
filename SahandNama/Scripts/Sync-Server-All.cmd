@echo off
chcp 65001 >nul
cd /d "%~dp0\.."
echo [سهند نما] در حال دانلود و همگام‌سازی تمامی گالری‌های آنلاین در سرور مرکزی...
if exist "BingWallpaperPro.exe" (
    "BingWallpaperPro.exe" --sync-all
    echo عملیات پایان یافت.
) else (
    echo خطای اجرایی: فایل BingWallpaperPro.exe یافت نشد.
)
pause
