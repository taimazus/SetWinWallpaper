<div dir="rtl" align="right" style="font-family: 'Vazirmatn', Tahoma, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.8;">

# راهنمای خط فرمان، اسکریپت‌ها و اتوماسیون (CLI & Automation Guide)

**نرم‌افزار سهند نما (Sahand Nama)**  
**نسخه:** 1.5.0  
**توسعه‌دهنده:** شرکت راهکار الکترونیک سهند ([https://irres.ir](https://irres.ir))

---

## ۱. آرگومان‌های خط فرمان (Command-Line Arguments)

فایل اجرایی `BingWallpaperPro.exe` قابلیت فراخوانی در حالت‌های مختلف بدون نمایش پنجره را دارد:

| پارامتر خط فرمان | عملکرد |
| :--- | :--- |
| `BingWallpaperPro.exe` | اجرای عادی با رابط کاربری گرافیکی (GUI) مدرن WPF |
| `BingWallpaperPro.exe --update` / `--update-quiet` | به‌روزرسانی سریع والپیپر و لاک‌اسکرین در پس‌زمینه بدون باز شدن پنجره |
| `BingWallpaperPro.exe --sync-all` | دانلود و همگام‌سازی تمامی گالری‌های آنلاین برای حالت مخزن سرور |
| `BingWallpaperPro.exe --install-schedule [HH:mm]` | ثبت یا به‌روزرسانی تسک زمان‌بندی روزانه کاربر جاری و زمان ورود |
| `BingWallpaperPro.exe --remove-schedule` | حذف تسک زمان‌بندی کاربر جاری از Task Scheduler ویندوز |
| `BingWallpaperPro.exe --install-service` | نصب و ثبت سرویس ویندوز `BingWallpaperProFeed` با دسترسی مدیر |
| `BingWallpaperPro.exe --remove-service` | حذف سرویس ویندوز `BingWallpaperProFeed` از سیستم |
| `BingWallpaperPro.exe --start-service` | راه‌اندازی (Start) سرویس ویندوز |
| `BingWallpaperPro.exe --stop-service` | متوقف‌سازی (Stop) سرویس ویندوز |
| `BingWallpaperPro.exe --diagnose` | اجرای آزمون سلامت سیستم و ذخیره گزارش در فایل لاگ |
| `BingWallpaperPro.exe --repair` | اجرای فرآیند کامل Auto-Repair (تنظیمات، رجیستری، زمان‌بندی و Spotlight) |
| `BingWallpaperPro.exe --widget` | اجرای مستقیم ویجت شیشه‌ای ساعت و تقویم خورشیدی دسکتاپ |
| `BingWallpaperPro.exe --toggle-icons` | پنهان/نمایان‌سازی فوری آیکون‌های دسکتاپ ویندوز |

---

## ۲. کلیدهای میانبر سراسری ویندوز (Global Hotkeys)

هنگام اجرای برنامه، کلیدهای میانبر زیر در کل محیط ویندوز فعال هستند:
* ⌨️ **`Win + Alt + W`** : دریافت و اعمال فوری والپیپر بعدی بدون نیاز به باز کردن پنجره برنامه.
* ⌨️ **`Win + Alt + S`** : افزودن سریع والپیپر دسکتاپ فعال به فهرست علاقه‌مندی‌ها (Favorites).

---

## ۲. اسکریپت‌های همراه نرم‌افزار

در پوشه `Scripts` همراه نرم‌افزار، اسکریپت‌های مدیریتی PowerShell قرار دارند:

### ۱. مدیریت سرویس ویندوز (`Manage-Service.ps1`)
نصب، استارت، استاپ یا حذف سرویس پس‌زمینه با دسترسی Administrator:
```powershell
# نصب و اجرای سرویس
.\Scripts\Manage-Service.ps1 -Action Install

# حذف سرویس
.\Scripts\Manage-Service.ps1 -Action Remove
```

### ۲. پیکربندی زمان‌بندی Task Scheduler
برنامه به طور خودکار تسکی با مشخصات زیر در ویندوز ایجاد می‌کند:
- **نام تسک:** `BingWallpaperPro-Daily-{UserSID}`
- **محرک‌ها (Triggers):**
  1. روزانه در ساعت تعیین‌شده کاربر (مثلاً `09:00`) با فرمت مستقل از تقویم محلی
  2. هنگام ورود به حساب کاربری (At Log on)
  3. اجرای فوری پس از روشن شدن سیستم در صورت خاموش بودن در ساعت مقرر (`StartWhenAvailable`)
- **اقدام (Action):** فراخوانی `BingWallpaperPro.exe --update`
- **شرایط اجرا:** اجرا در نشست تعاملی کاربر جاری با حفظ دسترسی بدون نیاز به پاپ‌آپ کنسول

---

## ۳. متغیرها و مسیرهای ذخیره‌سازی فایل‌ها

| عنوان پوشه | مسیر پیش‌فرض در ویندوز |
| :--- | :--- |
| تنظیمات و آرشیو | `%LOCALAPPDATA%\BingWallpaperPro\` |
| تصاویر دانلود شده | `%LOCALAPPDATA%\BingWallpaperPro\Images\` |
| نسخه‌های پشتیبان | `%LOCALAPPDATA%\BingWallpaperPro\Backups\` |
| فایل گزارش فعالیت | `%LOCALAPPDATA%\BingWallpaperPro\activity.log` |
| آخرین وضعیت اجرا | `%LOCALAPPDATA%\BingWallpaperPro\last-run.json` |
| آرشیو سرویس مشترک | `%ALLUSERSPROFILE%\BingWallpaperPro\Feed\` |

</div>
