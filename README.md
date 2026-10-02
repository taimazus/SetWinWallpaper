<div dir="rtl" align="right" style="font-family: 'Vazirmatn', Tahoma, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.8;">

# سهند نما | Sahand Nama (نسخه 1.7.0)

**سامانه جامع، هوشمند و سازمانی مدیریت و به‌روزرسانی تصاویر پس‌زمینه و صفحه قفل ویندوز**  
*طراحی و توسعه توسط شرکت راهکار الکترونیک سهند — [https://irres.ir](https://irres.ir)*

[![GitHub Release](https://img.shields.io/github/v/release/taimazus/SetWinWallpaper?color=52d1b2&label=Latest%20Release)](https://github.com/taimazus/SetWinWallpaper/releases/latest)
[![Tests](https://img.shields.io/badge/Tests-71%20Passed-52d1b2.svg)](docs/ARCHITECTURE.md)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20%7C%20Server-blue.svg)](docs/ENTERPRISE_DEPLOYMENT.md)
[![Architecture](https://img.shields.io/badge/Architecture-x64%20Standalone-orange.svg)](https://github.com/taimazus/SetWinWallpaper/releases/latest)
[![License](https://img.shields.io/badge/Font-SIL%20OFL%20Vazirmatn-brightgreen.svg)](BingWallpaperPro/Assets/Fonts/OFL.txt)

---

## 📥 دریافت و دانلود مستقیم خروجی ویندوز (Downloads)

برای استفاده از نرم‌افزار بدون نیاز به نصب هرگونه پیش‌نیاز یا دات‌نت، می‌توانید نسخه‌های آماده ۶۴بیتی را مستقیماً از بخش **Releases** گیت‌هاب دانلود کنید:

| نوع فایل | لینک دانلود مستقیم از گیت‌هاب | حجم تقریبی | مناسب برای |
| :--- | :--- | :--- | :--- |
| 🚀 **فایل اجرایی مستقل (EXE)** | [**دانلود SahandNama-v1.7.0-win-x64.exe**](https://github.com/taimazus/SetWinWallpaper/releases/download/v1.7.0/SahandNama-v1.7.0-win-x64.exe) | ~۱۴۰ مگابایت | اجرای فوری با یک کلیک بدون استخراج |
| 📦 **بسته پرتابل فشرده (ZIP)** | [**دانلود SahandNama-v1.7.0-win-x64.zip**](https://github.com/taimazus/SetWinWallpaper/releases/download/v1.7.0/SahandNama-v1.7.0-win-x64.zip) | ~۶۱ مگابایت | پکیج کامل شامل اسکریپت‌های PowerShell و راهنما |
| 🏷️ **صفحه انتشار نسخه‌ها** | [**مشاهده تمام Releaseها در گیت‌هاب**](https://github.com/taimazus/SetWinWallpaper/releases) | — | تاریخچه تغییرات و دسترسی به تمام نسخه‌ها |

---

## 🌟 معرفی پروژه

«سهند نما» یک نرم‌افزار مدرن، مستقل و بومی‌سازی‌شده با فونت فارسی **Vazirmatn** است که به کاربران و سازمان‌ها امکان می‌دهد تصاویر پس‌زمینه دسکتاپ و صفحه قفل ویندوز (ویندوز ۱۰، ۱۱ و سرورهای ۲۰۱۹ به بعد) را از معتبرترین منابع جهانی به طور خودکار دریافت کرده، در شبکه توزیع کنند، مصرف اینترنت را به حداقل برسانند و ایرادات ویندوز و Spotlight را با یک کلیک عیب‌یابی و خودترمیمی نمایند.

```mermaid
graph LR
    subgraph Sources ["منابع معتبر"]
        B[Bing & Bing Global]
        IR[ایران زیبا — طبیعت و آثار باستانی]
        N[NASA APOD & Library]
        H[ESA / Hubble & Webb]
        U[Unsplash 4K]
        W[Wikimedia POTD]
        E[USGS Earth as Art]
    end
    
    subgraph Engine ["هاب مرکزی سهند نما v1.6.0"]
        Core[موتور دریافت، کش سریع، استخراج پالت رنگی و کارت گرافیکی]
    end
    
    subgraph Targets ["مقاصد و امکانات"]
        D[دسکتاپ و مانیتورهای چندگانه]
        L[لاک‌اسکرین و Spotlight]
        WID[ویجت شیشه‌ای ساعت و تقویم خورشیدی]
        CLR[هماهنگی رنگ تم ویندوز Accent Color]
        HK[کلیدهای میانبر سراسری Win+Alt+W]
        S[مخزن سرور \\Server\Wallpapers]
    end
    
    Sources --> Core
    Core --> D
    Core --> L
    Core --> WID
    Core --> CLR
    Core --> HK
    Core --> S
```

---

## 📚 ساختار مستندات و دانشنامه (Documentation & Wiki)

برای مطالعه راهنماهای تفصیلی و تخصصی به بخش‌های زیر مراجعه فرمایید:

| مستند | شرح و موضوع |
| :--- | :--- |
| 📖 **[راهنمای جامع کاربری HTML (Guide.fa.html)](BingWallpaperPro/Guide.fa.html)** | راهنمای تعاملی، تصویری و کامل کاربر با طراحی شکیل Dark Mode |
| 📐 **[معماری و دیاگرام‌ها (docs/ARCHITECTURE.md)](docs/ARCHITECTURE.md)** | تحلیل معماری، الگوهای طراحی، دیاگرام‌های توالی و جریان داده‌ها |
| 🏢 **[استقرار سازمانی (docs/ENTERPRISE_DEPLOYMENT.md)](docs/ENTERPRISE_DEPLOYMENT.md)** | راهنمای هاب سرور، اشتراک شبکه (UNC Share)، تنظیمات GPO و کاهش پهنای باند |
| 🔍 **[عیب‌یابی Spotlight (docs/SPOTLIGHT_TROUBLESHOOTING.md)](docs/SPOTLIGHT_TROUBLESHOOTING.md)** | بررسی علل خرابی، پاک‌سازی کش و بازنشانی بسته‌های ContentDeliveryManager |
| 🌐 **[منابع و APIها (docs/SOURCES_AND_API.md)](docs/SOURCES_AND_API.md)** | جزئیات فیدها، کیفیت 4K، پروتکل‌های امنیتی و الگوریتم یکتاسازی SHA-256 |
| ⌨️ **[خط فرمان و اسکریپت‌ها (docs/CLI_AND_SCRIPTS.md)](docs/CLI_AND_SCRIPTS.md)** | آرگومان‌های خاموش (Silent)، کلیدهای میانبر و سرویس ویندوز |
| 🏛️ **[دانشنامه پروژه (wiki/Home.md)](wiki/Home.md)** | صفحه اصلی ویکی شامل راهنمای کاربری، ادمین و توسعه‌دهندگان |

---

## ✨ امکانات برجسته نسخه 1.7.0

1. 🪟 **ویجت دسکتاپ شیشه‌ای مات واقعی (True Acrylic Frosted Glass):**
   - افکت بلوری و شیشه‌ای عمیق مات بر پایه API بومی `SetWindowCompositionAttribute` و شتاب‌یافته با GPU.
   - ساعت زنده دیجیتال، تقویم خورشیدی هجری شمسی، نام منظره، سنجاق (📌)، تعویض آنی والپیپر (🎲)، نشان‌کردن (⭐) و استخراج رنگ (🎨).
2. 🔔 **سینی کنار ساعت ویندوز و خروج دوگانه (System Tray & Dual Exit):**
   - دکمه `✕` برنامه را نمی‌بندد، بلکه پنجره را به سینی ویندوز فرستاده و ویجت و زمان‌بندی را زنده نگه می‌دارد.
   - منوی راست‌کلیک کامل فارسی در کنار ساعت با دکمه **«🚪 خروج کامل از برنامه»**.
3. 🌄 **۵ منبع جدید و خیره‌کننده ۴K:**
   - والپیپرهای برگزیده Wallhaven 4K/8K، آثار موزه هنر شیکاگو، شگفتی‌های نشنال جئوگرافیک، هنر سایبرپانک و معماری مدرن ۴K.
4. 🎛️ **انتخاب مستقل متد برای دسکتاپ و لاک‌اسکرین:**
   - امکان تنظیم همزمان حالت روزانه برای دسکتاپ و رندوم برای صفحه قفل به صورت کاملاً تفکیک‌شده.
5. ⌨️ **کلیدهای میانبر سراسری ویندوز (Global Hotkeys):**
   - کلید `Win + Alt + W`: دریافت و اعمال فوری والپیپر بعدی بدون نیاز به باز کردن پنجره برنامه.
   - کلید `Win + Alt + S`: افزودن سریع تصویر فعال به فهرست علاقه‌مندی‌ها.
6. 🏛️ **کالکشن اختصاصی «ایران زیبا» (Iran Nature & Heritage):**
   - دسترسی به تصاویر باکیفیت از طبیعت شگفت‌انگیز و میراث باستانی ایران.
7. 🖥️ **پشتیبانی از چندین مانیتور (Multi-Monitor Customization):**
   - امکان اعمال تصاویر روی تمام مانیتورهای متصل به سیستم از طریق رابط پیشرفته Win32 `IDesktopWallpaper`.
8. 🎨 **هماهنگ‌سازی خودکار رنگ تم ویندوز (Accent Color Sync):**
   - استخراج هوشمند رنگ غالب تصویر روز و تنظیم خودکار Accent Color ویندوز متناسب با منظره.
9. 📤 **تولید کارت گرافیکی اشتراک‌گذاری (Social Share Card Generator):**
   - ساخت کارت گرافیکی باکیفیت و آماده انتشار در شبکه‌های اجتماعی همراه با تاریخ شمسی و برند سهند نما.
10. 🧹 **حالت دسکتاپ تمیز (Clean Desktop Icons):**
    - قابلیت پنهان/نمایان‌سازی سریع آیکون‌های دسکتاپ برای لذت بردن از والپیپر به عنوان قاب عکس دیجیتال.
11. ⏰ **سامانه زمان‌بندی هوشمند چندرویدادی (Multi-Trigger Scheduler):**
    - اجرای مستقل از Culture در ساعت مقرر (`Daily`) و هنگام ورود کاربر (`AtLogOn`) با قابلیت `StartWhenAvailable`.
12. 🏢 **مخزن مرکزی سرور (Enterprise Server Hub):**
    - دانلود متمرکز تمام منابع روزانه در سرور و تغذیه کلاینت‌ها از مسیر شبکه `\\Server\Wallpapers` بدون مصرف اینترنت.
13. 🛠️ **مرکز عیب‌یابی و خودترمیمی خودکار:**
    - بررسی جامع رجیستری، سرویس‌ها، تسک‌ها، کش و بازنشانی کامل Windows Spotlight با یک کلیک.

---

## 🛠️ کامپایل و اجرای آزمون‌ها

```powershell
# اجرای ۷۱ آزمون فنی و اعتبارسنجی
dotnet run --project BingWallpaperPro.Tests\BingWallpaperPro.Tests.csproj

# ساخت و انتشار خروجی نهایی مستقل ۶۴بیتی
dotnet publish BingWallpaperPro\BingWallpaperPro.csproj -c Release -r win-x64 --self-contained true -o artifacts\standalone
```

</div>
