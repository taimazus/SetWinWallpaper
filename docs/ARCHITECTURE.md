<div dir="rtl" align="right" style="font-family: 'Vazirmatn', Tahoma, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.8;">

# معماری فنی و دیاگرام سیستم — سهند نما (Sahand Nama Architecture)

**نسخه:** 1.8.1
**توسعه‌دهنده:** شرکت راهکار الکترونیک سهند ([https://irres.ir](https://irres.ir))  
**فناوری:** .NET 10.0 (C# 13), WPF (Windows Presentation Foundation), Win32 Native P/Invoke, DWM Acrylic Blur, PowerShell Native Bridge, SCM Windows Service Dispatcher.

---

## ۱. نمای کلی معماری (High-Level Architecture)

نرم‌افزار سهند نما بر پایه الگوی معماری ماژولار با حداقل وابستگی جانبی (Zero Third-Party Dependency) طراحی شده است. تمام عملیات شبکه، تجزیه داده‌ها، بهینه‌سازی تصاویر، ویجت شیشه‌ای و تعامل با ویندوز از طریق کامپوننت‌های بهینه‌سازی‌شده درون برنامه انجام می‌شود.

```mermaid
graph TD
    UI["رابط کاربری مدرن WPF<br/>(MainWindow & Theme.xaml)"] --> Cache["کش سریع حافظه‌ای تصاویر بندانگشتی<br/>(ThumbnailConverter Concurrent Cache)"]
    UI --> Controller["کنترلر مرکزی و موتور پس‌زمینه<br/>(WallpaperEngine)"]
    
    WID["ویجت شیشه‌ای دسکتاپ<br/>(DesktopWidgetWindow Acrylic Blur)"] --> WService["سرویس زنده آب‌وهوا<br/>(WeatherService Open-Meteo)"]
    WID --> HMonitor["پایشگر سخت‌افزار سیستم<br/>(HardwareMonitor CPU/RAM/Battery)"]
    WID --> DPin["الصاق به شل دسکتاپ<br/>(DesktopPinning WorkerW)"]
    
    TRY["مدیریت سینی ویندوز<br/>(TrayManager Shell_NotifyIcon)"] --> AppCtrl["چرخه حیات برنامه و خروج دوگانه<br/>(App.xaml.cs Lifecycle)"]

    subgraph Core Engine ["موتور مرکزی (Core Engine v1.8.1)"]
        Controller --> Prefs["مدیریت تنظیمات و ذخیره‌سازی اتمیک<br/>(Preferences & Store)"]
        Controller --> LockCoord["مدیریت قفل هم‌زمانی با تلاش مجدد<br/>(Store.AcquireLockAsync)"]
        Controller --> Catalog["کاتالوگ و بارگذاری منابع<br/>(SourceCatalog & SourceHttp)"]
        Controller --> Dedup["سامانه یکتا‌سازی هش SHA-256<br/>(Deduplication Engine)"]
        Controller --> ColorSync["استخراج پالت و تطبیق رنگ تم<br/>(Dominant Color & Accent Sync)"]
        Controller --> SocialCard["سازنده کارت گرافیکی شبکه‌های اجتماعی<br/>(CardGenerator DrawingVisual)"]
    end

    subgraph Data Sources ["منابع تصاویر و داده‌ها (Data Sources)"]
        Catalog --> Bing["Bing Daily & Bing Global"]
        Catalog --> Iran["ایران زیبا (IranNature)"]
        Catalog --> Wallhaven["Wallhaven (خروجی تا ضلع 3840) Wallpapers"]
        Catalog --> Museum["Art Institute collection via Wikimedia Commons"]
        Catalog --> NatGeo["Wikimedia Featured & Wallhaven"]
        Catalog --> NASA["NASA Image of the Day & Library"]
        Catalog --> Hubble["ESA / Hubble & Webb"]
        Catalog --> Unsplash["Unsplash & Picsum 4K"]
        Catalog --> Wiki["Wikimedia POTD"]
        Catalog --> USGS["USGS Earth as Art"]
        Catalog --> UNC["Enterprise UNC Share"]
    end

    subgraph Windows Interop ["تعامل با سیستم‌عامل (Windows Integration)"]
        Controller --> WinAPI["تغییر والپیپر دسکتاپ و چند مانیتور<br/>(SystemParametersInfo & IDesktopWallpaper)"]
        Controller --> LockAPI["سیاست لاک‌اسکرین ویندوز<br/>(PersonalizationCSP & WinRT/PowerShell)"]
        Controller --> TaskSched["زمان‌بندی هوشمند چندرویدادی<br/>(Windows Task Scheduler - Daily + Logon)"]
        Controller --> WinService["سرویس ویندوز هماهنگ با SCM<br/>(BingWallpaperProFeed Service)"]
        Controller --> Hotkeys["کلیدهای میانبر سراسری ویندوز<br/>(RegisterHotKey Win32)"]
        Controller --> DwmBlur["ترکیب بلور اکریلیک دسکتاپ<br/>(SetWindowCompositionAttribute)"]
        Controller --> SpotRepair["تعمیر عمیق Spotlight<br/>(ContentDeliveryManager & AppX)"]
    end
```

---

## ۲. چرخه حیات ویجت شیشه‌ای و سینی سیستم (Desktop Widget & System Tray Lifecycle)

```mermaid
sequenceDiagram
    autonumber
    participant User as کاربر / ویندوز
    participant Main as پنجره اصلی (MainWindow)
    participant App as هسته برنامه (App.xaml.cs)
    participant Tray as سینی ویندوز (TrayManager)
    participant Widget as ویجت دسکتاپ (DesktopWidgetWindow)
    participant Win32 as توابع سیستمی Win32 / DWM

    User->>Main: بستن پنجره با دکمه [✕]
    Main->>App: لغو بسته شدن کامل (Cancel Close Event)
    Main->>Main: پنهان‌سازی پنجره (Hide Window)
    Main->>Tray: نمایش بالون راهنما در سینی ویندوز
    Note over App,Tray: برنامه در سینی فعال می‌ماند

    App->>Widget: تداوم نمایش ویجت روی دسکتاپ
    Widget->>Win32: اعمال بلور شیشه‌ای (EnableAcrylicBlur)
    Widget->>Win32: الصاق به پنجره دسکتاپ (WorkerW Pinning)

    User->>Tray: راست‌کلیک روی آیکون سینی ویندوز
    Tray-->>User: نمایش منوی فارسی (تغییر والپیپر، آب‌وهوا، ویجت، خروج کامل)
    
    alt انتخاب «نمایش پنجره اصلی»
        User->>Tray: کلیک روی نمایش پنجره
        Tray->>App: ShowMainWindow()
        App->>Main: Show & Activate Window
    else انتخاب «خروج کامل از برنامه»
        User->>Tray: کلیک روی خروج کامل
        Tray->>App: ExitApplication()
        App->>Tray: حذف آیکون (Shell_NotifyIcon NIM_DELETE)
        App->>Win32: آزادسازی کلیدهای میانبر (UnregisterHotKey)
        App->>App: خاتمه تمام فرآیندها
    end
```

---

## ۳. چرخه حیات زمان‌بندی و سرویس ویندوز (Service & Scheduling Lifecycle)

```mermaid
stateDiagram-v2
    [*] --> Idle : استقرار برنامه

    state "زمان‌بندی کاربر (Task Scheduler)" as SchedTask {
        Idle --> TriggerAtLogon : رویداد ورود کاربر به ویندوز
        Idle --> TriggerDaily : ساعت معین روزانه (HH:mm)
        Idle --> TriggerWake : روشن شدن سیستم بعد از ساعت مقرر (StartWhenAvailable)
        
        TriggerAtLogon --> ExecUpdate : فراخوانی SahandNama.exe --update
        TriggerDaily --> ExecUpdate
        TriggerWake --> ExecUpdate

        ExecUpdate --> CheckNetwork : بررسی دسترسی شبکه
        CheckNetwork --> FetchOnline : آنلاین بودن (دریافت منبع انتخابی)
        CheckNetwork --> FallbackOffline : آفلاین بودن (استفاده از آرشیو محلی/کش)
        FetchOnline --> ApplyWallpaper : اعمال روی دسکتاپ/لاک‌اسکرین
        FallbackOffline --> ApplyWallpaper
        ApplyWallpaper --> LogResult : ثبت در activity.log و last-run.json
    }

    state "سرویس سرور (Windows Service - SCM)" as SCMService {
        Idle --> ServiceStart : شروع سرویس BingWallpaperProFeed
        ServiceStart --> ServiceLoop : آغاز حلقه همگام‌سازی ناهمگام (Non-blocking)
        ServiceLoop --> SyncAll : دانلود همگانی منابع (SyncAllOnlineSourcesAsync)
        SyncAll --> StoreShared : ذخیره در مخزن شبکه (ProgramData\BingWallpaperPro\Feed)
        StoreShared --> SleepInterval : انتظار ۱۲ ساعته (یا ۱۰ دقیقه در صورت قطعی شبکه)
        SleepInterval --> ServiceLoop
    }
```

---

## ۴. دیاگرام توزیع شبکه سرور و کلاینت‌ها (Enterprise Hub & Distribution)

```mermaid
sequenceDiagram
    autonumber
    participant Internet as اینترنت / منابع معتبر جهانی
    participant Server as ویندوز سرور (Enterprise Hub)
    participant Share as پوشه اشتراکی شبکه (\\Server\Wallpapers)
    participant Client1 as کلاینت ۱ (حسابداری)
    participant Client2 as کلاینت ۲ (فنی و مهندسی)

    Note over Server: اجرای زمان‌بندی روزانه یا سرویس ویندوز
    Server->>Internet: دریافت تمامی گالری‌ها (SyncAllOnlineSourcesAsync)
    Internet-->>Server: استریم تصاویر 4K و متادیتا
    Server->>Server: محاسبه هش SHA-256 و حذف فایل‌های تکراری
    Server->>Share: ذخیره در پوشه محلی و به‌روزرسانی archive.json
    
    Note over Client1,Client2: ورود کاربر یا ساعت کار روزانه
    Client1->>Share: بررسی آخرین تصاویر روز بدون مصرف اینترنت
    Share-->>Client1: بارگذاری سریع فایل از طریق LAN
    Client1->>Client1: اعمال بر روی دسکتاپ و لاک‌اسکرین
    
    Client2->>Share: خواندن تصویر متناظر با منبع انتخابی
    Share-->>Client2: انتقال تصویر از شبکه محلی
    Client2->>Client2: تنظیم والپیپر و لاگ موفقیت
```

---

## ۵. دیاگرام جریان عیب‌یابی و رفع خودکار ایرادات (Diagnostics & Auto-Repair Flow)

```mermaid
flowchart TD
    Start([شروع عیب‌یابی یا تعمیر]) --> CheckSys{بررسی ۲۲ مولفه سیستم}
    
    CheckSys --> Test1[بررسی یکپارچگی فایل تنظیمات settings.json]
    CheckSys --> Test2[بررسی اتصال منابع آنلاین و DNS]
    CheckSys --> Test3[بررسی کلیدهای رجیستری و گروپ پالیسی]
    CheckSys --> Test4[بررسی وضعیت Task Scheduler، دسترسی‌ها و فرآیند اجرایی]
    CheckSys --> Test5[بررسی وضعیت سرویس ویندوز BingWallpaperProFeed]
    CheckSys --> Test6[بررسی کش و پکیج‌های Spotlight]
    CheckSys --> Test7[بررسی فایل‌های تکراری و صفر بایتی با هش SHA-256]

    Test1 & Test2 & Test3 & Test4 & Test5 & Test6 & Test7 --> GenReport[تولید گزارش جامع سلامت با تفکیک رنگی]

    GenReport --> AutoRepair{درخواست رفع خودکار ایرادات؟}
    AutoRepair -- خیر --> EndReport([نمایش یا صدور خروجی گزارش])
    
    AutoRepair -- بله --> Backup[پشتیبان JSON خراب پیش از جایگزینی]
    Backup --> FixSettings[تعمیر ساختار تنظیمات و اعمال مقادیر پیش‌فرض امن]
    FixSettings --> PurgeCache[حذف فایل‌های خراب و ناقص کش]
    FixSettings --> FixSched[ثبت مجدد و تصحیح تسک روزانه با رویداد ورود]
    FixSched --> FixSpotlight[بازنشانی بسته‌های ContentDeliveryManager و کلیدهای Spotlight]
    FixSpotlight --> DedupPurge[پاک‌سازی فایل‌های تکراری با الگوریتم SHA-256]
    DedupPurge --> FinalReport([تولید گزارش تایید سلامت و پایان موفقیت‌آمیز])
```

---

## ۶. ماتریس آزمون‌های اعتبارسنجی خودکار (Automated Verification Matrix)

تمام **۷۵ تست پروژه** به صورت مداوم وضعیت‌های زیر را ارزیابی می‌کنند:

1. **امنیت شبکه و ضد نفوذ:** رد کردن تمام URLهای غیرمجاز، پروتکل‌های ناامن، پورت‌های غیررسمی و تلاش‌های SSRF.
2. **عملیات اتمیک فایل و هماهنگی قفل‌ها:** تضمین عدم رها شدن فایل‌های `.tmp`، بازیافت خطای هم‌زمانی و تلاش مجدد با `AcquireLockAsync`.
3. **صحت دکودینگ تصویر و عدم قفل ماندن فایل:** دکود مستقیم، آزاد شدن فوری Handle فایل و کش در حافظه رم.
4. **تطبیق فیدهای بین‌المللی:** پارس کردن فیدهای NASA Image of the Day, Wikimedia POTD, ESA Hubble, Picsum/Unsplash, USGS, Wallhaven, MuseumArt.
5. **مسیریابی هوشمند و تفکیک دسکتاپ و لاک‌اسکرین:** پشتیبانی از حالت‌های مستقل، همزمان، تفکیک مناطق جغرافیایی و کالکشن ایران زیبا.
6. **محیط کاملاً آفلاین کلاینت:** آزمون بدون اینترنت و واکشی مستقیم از Share شبکه.
7. **طراحی بصری و فونت فارسی:** تایید بارگذاری قلم فارسی Vazirmatn، جهت‌بندی RTL، استایل شیشه‌ای `Theme.xaml` و آیکون اختصاصی سهند نما.
8. **ویجت دسکتاپ، آب‌وهوا و سخت‌افزار:** پایش حافظه RAM، محاسبه دمای شهرها، فرمت تاریخ هجری خورشیدی، استخراج پالت رنگ تم ویندوز و ساخت کارت شبکه اجتماعی.

</div>
