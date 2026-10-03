@echo off
chcp 65001 >nul
cd /d "%~dp0\.."
echo ========================================================
echo [سهند نما] تعمیر تنظیمات، آرشیو و زمان‌بندی برنامه (بازنشانی Spotlight از رابط کاربری)
echo ========================================================
if exist "SahandNama.exe" (
    "SahandNama.exe" --repair
    echo.
    echo فرآیند تعمیر پایان یافت. گزارش در فایل activity.log ثبت شد.
) else (
    echo خطای اجرایی: فایل SahandNama.exe یافت نشد.
)
echo.
pause
