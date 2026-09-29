<div dir="rtl" align="right" style="font-family: 'Vazirmatn', Tahoma, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.8;">

# راهنمای توسعه‌دهندگان (Developer Guide)

این مستند شامل اصول توسعه، معماری کد، نحوه کامپایل، اجرای مجموعه آزمون‌ها و راهنمای افزودن منابع جدید به پروژه **سهند نما (Sahand Nama)** است.

---

## ۱. پیش‌نیازهای توسعه (Prerequisites)

- .NET 10.0 SDK یا بالاتر
- Visual Studio 2022 / 2025 یا VS Code به همراه افزونه C# Dev Kit
- سیستم‌عامل Windows 10/11 x64 یا Windows Server

---

## ۲. ساختار پروژه و ماژول‌ها

```
SetWinWallpaper/
├── BingWallpaperPro/                # پروژه اصلی WPF (.NET 10)
│   ├── Assets/                      # فونت وزیزمتن، آیکون و لوگو
│   ├── Scripts/                     # اسکریپت‌های PowerShell
│   ├── Brand.cs                     # مشخصات شرکت و متغیرهای برندینگ
│   ├── Core.cs                      # موتور داده، تنظیمات و WallpaperEngine
│   ├── Diagnostics.cs               # موتور عیب‌یابی و خودترمیمی
│   ├── DownloadService.cs           # سرویس ویندوز SCM جهت دانلود متمرکز
│   ├── Presentation.cs              # مبدل‌های UI و کش حافظه‌ای تصاویر
│   ├── Sources.cs                   # کاتالوگ منابع، دانلود، فید و هشینگ
│   ├── WindowsIntegration.cs        # تعاملات Win32، زمان‌بندی و بازنشانی Spotlight
│   ├── MainWindow.xaml              # ساختار گرافیکی و تم راست‌به‌چپ
│   └── Guide.fa.html                # راهنمای تعاملی کاربر به زبان فارسی
├── BingWallpaperPro.Tests/          # مجموعه ۵۷ آزمون واحد و یکپارچه‌سازی
├── docs/                            # مستندات معماری و استقرار سازمانی
├── wiki/                            # دانشنامه و راهنماهای پروژه
└── artifacts/                       # خروجی‌های کامپایل و فایل زیپ انتشار
```

---

## ۳. اجرای تست‌های خودکار (Automated Testing)

پروژه دارای **۵۷ تست جامع** جهت اطمینان از صحت اعتبارسنجی URLها، امنیت شبکه، عدم تزریق اسکریپت در PowerShell، هشینگ SHA-256، دانلود امن، بازنشانی Spotlight، کش در حافظه، خواندن فونت و عدم کرش در شرایط خطا است:

```powershell
dotnet run --project BingWallpaperPro.Tests\BingWallpaperPro.Tests.csproj
```

**خروجی مورد انتظار:**
```
57 checks passed. Live lock screen application: False. No desktop, policy, task, repair or service was applied.
```

---

## ۴. انتشار بسته مستقل (Publishing Standalone Binary)

برای تولید فایل اجرایی ۶۴ بیتی مستقل و کامپکت بدون نیاز به نصب دات‌نت:

```powershell
dotnet publish BingWallpaperPro\BingWallpaperPro.csproj -c Release -r win-x64 --self-contained true -o artifacts\standalone
```

---

## ۵. راهنمای افزودن منبع آنلاین جدید (Adding a New Source)

برای افزودن یک گالری تصویر جدید:
1. شناسه و نام منبع را در آرایه `SourceCatalog.Options` در [Sources.cs](file:///f:/Projects/SetWinWallpaper/BingWallpaperPro/Sources.cs) تعریف کنید.
2. هاست دامنه منبع را در متد `SourceHttp.Validate` اضافه کنید (سیاست Allowlist سخت‌گیرانه).
3. متد پارسر متناسب (JSON یا RSS/XML) را پیاده‌سازی کرده و در `FetchAsync` فراخوانی نمایید.
4. تست اعتبارسنجی منبع جدید را در [BingWallpaperPro.Tests/Program.cs](file:///f:/Projects/SetWinWallpaper/BingWallpaperPro.Tests/Program.cs) اضافه کنید.

</div>
