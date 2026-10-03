<div dir="rtl" align="right" style="font-family: 'Vazirmatn', Tahoma, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.8;">

# راهنمای توسعه‌دهندگان (Developer Guide)

این مستند شامل اصول توسعه، معماری کد، نحوه کامپایل، اجرای مجموعه آزمون‌ها و راهنمای افزودن قابلیت‌های جدید به پروژه **سهند نما (Sahand Nama)** است.

---

## ۱. پیش‌نیازهای توسعه (Prerequisites)

- .NET 10.0 SDK یا بالاتر
- Visual Studio 2022 / 2025 یا VS Code به همراه افزونه C# Dev Kit
- سیستم‌عامل Windows 10/11 x64 یا Windows Server

---

## ۲. ساختار پروژه و ماژول‌ها

```
SetWinWallpaper/
├── SahandNama/                      # پروژه اصلی WPF (.NET 10)
│   ├── Assets/                            # فونت وزیزمتن، آیکون و لوگو
│   ├── Scripts/                           # اسکریپت‌های PowerShell
│   ├── Brand.cs                           # مشخصات شرکت و متغیرهای برندینگ
│   ├── Core.cs                            # موتور داده، تنظیمات، قفل‌ها و WallpaperEngine
│   ├── DesktopPinning.cs                  # اتصال ویجت به شل دسکتاپ و مانیتورها
│   ├── DesktopWidgetWindow.xaml           # ویجت شیشه‌ای بلور مات دسکتاپ
│   ├── DesktopWidgetWindow.xaml.cs        # منطق تعاملی، آب‌وهوا، ساعت و سخت‌افزار
│   ├── Diagnostics.cs                     # موتور عیب‌یابی و خودترمیمی جامع
│   ├── DownloadService.cs                 # سرویس ویندوز SCM جهت دانلود متمرکز
│   ├── HardwareMonitor.cs                 # پایش بومی حافظه RAM، پردازنده و باتری
│   ├── Presentation.cs                    # مبدل‌های UI، فونت و کش تصاویر
│   ├── Sources.cs                         # کاتالوگ منابع، دانلود، فید و هشینگ
│   ├── Theme.xaml                         # استایل‌های شیشه‌ای GlassPanel و رنگ‌ها
│   ├── TrayManager.cs                     # مدیریت آیکون سینی ویندوز و منو
│   ├── WeatherService.cs                  # وب‌سرویس وضعیت آب‌وهوا (Open-Meteo)
│   ├── WindowsIntegration.cs              # تعاملات Win32، زمان‌بندی، کلیدهای میانبر و Spotlight
│   ├── MainWindow.xaml                    # رابط کاربری اصلی با فونت فارسی
│   └── Guide.fa.html                      # راهنمای تعاملی کاربر
├── SahandNama.Tests/                # مجموعه ۷۵ آزمون خودکار واحد و یکپارچه‌سازی
├── docs/                                  # مستندات معماری، API، استقرار و عیب‌یابی
├── wiki/                                  # دانشنامه و راهنماهای پروژه
└── artifacts/                             # خروجی‌های کامپایل و فایل‌های انتشار
```

---

## ۳. اجرای تست‌های خودکار (Automated Testing)

پروژه دارای **۷۵ تست جامع** جهت اطمینان از صحت اعتبارسنجی URLها، امنیت شبکه، عدم تزریق اسکریپت در PowerShell، هشینگ SHA-256، دانلود امن، بازنشانی Spotlight، کش در حافظه، خواندن فونت، هماهنگی قفل‌ها با تلاش مجدد، سرویس آب‌وهوا، مانیتورینگ سخت‌افزار و پایداری تم است:

```powershell
dotnet run --project SahandNama.Tests\SahandNama.Tests.csproj
```

**خروجی مورد انتظار:**
```
نتیجهٔ شمارش جاری بررسی‌ها در docs/audit-report.md ثبت می‌شود.
```

---

## ۴. انتشار بسته مستقل (Publishing Standalone Binary)

برای تولید فایل اجرایی ۶۴ بیتی مستقل و کامپکت بدون نیاز به نصب دات‌نت:

```powershell
dotnet publish SahandNama\SahandNama.csproj -c Release -r win-x64 --self-contained true -o artifacts\standalone
```

---

## ۵. راهنمای افزودن منبع آنلاین جدید (Adding a New Source)

برای افزودن یک گالری تصویر جدید:
1. شناسه و نام منبع را در آرایه `SourceCatalog.Options` در [Sources.cs](file:///f:/Projects/SetWinWallpaper/SahandNama/Sources.cs) تعریف کنید.
2. هاست دامنه منبع را در متد `SourceHttp.Validate` اضافه کنید (سیاست Allowlist سخت‌گیرانه).
3. متد پارسر متناسب (JSON یا RSS/XML) را پیاده‌سازی کرده و در `FetchAsync` فراخوانی نمایید.
4. تست اعتبارسنجی منبع جدید را در [SahandNama.Tests/Program.cs](file:///f:/Projects/SetWinWallpaper/SahandNama.Tests/Program.cs) اضافه کنید.

</div>
