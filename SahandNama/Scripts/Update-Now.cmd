@echo off
chcp 65001 >nul
cd /d "%~dp0\.."
echo [سهند نما] در حال به‌روزرسانی سریع تصاویر پس‌زمینه و قفل...
if exist "BingWallpaperPro.exe" (
    start "" "BingWallpaperPro.exe" --update-quiet
) else (
    echo خطای اجرایی: فایل BingWallpaperPro.exe یافت نشد.
)
