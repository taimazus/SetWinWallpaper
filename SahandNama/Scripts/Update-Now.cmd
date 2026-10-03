@echo off
chcp 65001 >nul
cd /d "%~dp0\.."
echo [سهند نما] در حال به‌روزرسانی سریع تصاویر پس‌زمینه و قفل...
if exist "SahandNama.exe" (
    start "" "SahandNama.exe" --update-quiet
) else (
    echo خطای اجرایی: فایل SahandNama.exe یافت نشد.
)
