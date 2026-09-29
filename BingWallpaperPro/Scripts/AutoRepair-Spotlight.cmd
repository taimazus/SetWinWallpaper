@echo off
chcp 65001 >nul
cd /d "%~dp0\.."
echo ========================================================
echo [سهند نما] شروع فرآیند خودکار عیب‌یابی و تعمیر Spotlight
echo ========================================================
if exist "BingWallpaperPro.exe" (
    "BingWallpaperPro.exe" --repair
    echo.
    echo فرآیند تعمیر پایان یافت. گزارش در فایل activity.log ثبت شد.
) else (
    echo خطای اجرایی: فایل BingWallpaperPro.exe یافت نشد.
)
echo.
pause
