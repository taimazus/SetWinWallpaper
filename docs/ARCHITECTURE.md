# معماری فنی و دیاگرام سیستم — سهند نما (Sahand Nama Architecture)

**نسخه:** 1.5.0  
**توسعه‌دهنده:** شرکت راهکار الکترونیک سهند ([https://irres.ir](https://irres.ir))  
**فناوری:** .NET 10.0 (C# 13), WPF (Windows Presentation Foundation), PowerShell Native Bridge, Win32 Native Interop, SCM Service Dispatcher.

---

## ۱. نمای کلی معماری (High-Level Architecture)

نرم‌افزار سهند نما بر پایه الگوی معماری ماژولار با حداقل وابستگی جانبی (Zero Third-Party Dependency) طراحی شده است. تمام عملیات شبکه، تجزیه داده‌ها، بهینه‌سازی تصاویر و تعامل با ویندوز از طریق کامپوننت‌های بهینه‌سازی‌شده درون برنامه انجام می‌شود.

```mermaid
graph TD
    UI["رابط کاربری مدرن WPF<br/>(MainWindow & Theme)"] --> Cache["کش سریع حافظه‌ای تصاویر بندانگشتی<br/>(Concurrent In-Memory Cache)"]
    UI --> Controller["کنترلر مرکزی و موتور پس‌زمینه<br/>(WallpaperEngine)"]
    
    subgraph Core Engine ["موتور مرکزی (Core Engine v1.5.0)"]
        Controller --> Prefs["مدیریت تنظیمات و ذخیره‌سازی اتمیک<br/>(Preferences & Store)"]
        Controller --> Catalog["کاتالوگ و بارگذاری منابع<br/>(SourceCatalog & SourceHttp)"]
        Controller --> Dedup["سامانه یکتا‌سازی هش SHA-256<br/>(Deduplication Engine)"]
    end

    subgraph Data Sources ["منابع تصاویر (Data Sources)"]
        Catalog --> Bing["Bing Daily & Bing Global"]
        Catalog --> NASA["NASA APOD & Library"]
        Catalog --> Hubble["ESA / Hubble & Webb"]
        Catalog --> Unsplash["Unsplash & Picsum 4K"]
        Catalog --> Wiki["Wikimedia POTD"]
        Catalog --> USGS["USGS Earth as Art"]
        Catalog --> UNC["Enterprise UNC Share"]
    end

    subgraph Windows Interop ["تعامل با سیستم‌عامل (Windows Integration)"]
        Controller --> WinAPI["تغییر والپیپر دسکتاپ<br/>(SystemParametersInfo Win32)"]
        Controller --> LockAPI["سیاست لاک‌اسکرین ویندوز<br/>(PersonalizationCSP & WinRT/PowerShell)"]
        Controller --> TaskSched["زمان‌بندی هوشمند چندرویدادی<br/>(Windows Task Scheduler - Daily + Logon)"]
        Controller --> WinService["سرویس ویندوز هماهنگ با SCM<br/>(BingWallpaperProFeed Service)"]
        Controller --> SpotRepair["تعمیر عمیق Spotlight<br/>(ContentDeliveryManager & AppX)"]
    end
```

---

## ۲. چرخه حیات و معماری سرویس و زمان‌بندی (Service & Scheduling Lifecycle)

در نسخه 1.5.0، سیستم زمان‌بندی و سرویس ویندوز کاملاً مستقل از Culture و دسترسی شبکه طراحی شده است:

```mermaid
stateDiagram-v2
    [*] --> Idle : استقرار برنامه

    state "زمان‌بندی کاربر (Task Scheduler)" as SchedTask {
        Idle --> TriggerAtLogon : رویداد ورود کاربر به ویندوز
        Idle --> TriggerDaily : ساعت معین روزانه (HH:mm)
        Idle --> TriggerWake : روشن شدن سیستم بعد از ساعت مقرر (StartWhenAvailable)
        
        TriggerAtLogon --> ExecUpdate : فراخوانی BingWallpaperPro.exe --update
        TriggerDaily --> ExecUpdate
        TriggerWake --> ExecUpdate

        ExecUpdate --> CheckNetwork : بررسی دسترسی شبکه
        CheckNetwork --> FetchOnline : آنلاین بودن (دریافت منبع انتخابی)
        CheckNetwork --> FallbackOffline : آفلاین بودن (استفاده از آرشیو محلی/کش)
        FetchOnline --> ApplyWallpaper : اعمال روی دسکتاپ/لاک‌اسکرین
        FallbackOffline --> ApplyWallpaper
        ApplyWallpaper --> LogResult : ثبت در last-run.json و activity.log
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

## ۳. دیاگرام توزیع شبکه سرور و کلاینت‌ها (Enterprise Hub & Distribution)

در شبکه‌های سازمانی، سرور مرکزی به عنوان **Enterprise Hub** عمل کرده و کلاینت‌ها بدون مصرف اینترنت، تصاویر را مستقیماً از پوشه اشتراکی شبکه دریافت می‌کنند:

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

## ۴. دیاگرام جریان عیب‌یابی و رفع خودکار ایرادات (Diagnostics & Auto-Repair Flow)

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
    
    AutoRepair -- بله --> Backup[ایجاد فایل پشتیبان از تنظیمات و رجیستری]
    Backup --> FixSettings[تعمیر ساختار تنظیمات و اعمال مقادیر پیش‌فرض امن]
    FixSettings --> PurgeCache[حذف فایل‌های خراب و ناقص کش]
    PurgeCache --> FixSched[ثبت مجدد و تصحیح تسک روزانه با رویداد ورود]
    FixSched --> FixSpotlight[بازنشانی بسته‌های ContentDeliveryManager و کلیدهای Spotlight]
    FixSpotlight --> DedupPurge[پاک‌سازی فایل‌های تکراری با الگوریتم SHA-256]
    DedupPurge --> FinalReport([تولید گزارش تایید سلامت و پایان موفقیت‌آمیز])
```

---

## ۵. ماتریس آزمون‌های اعتبارسنجی خودکار (Automated Verification Matrix)

تمام ۵۷ تست پروژه به صورت مداوم وضعیت زیر را ارزیابی می‌کنند:

1. **امنیت شبکه و ضد نفوذ:** رد کردن تمام URLهای غیرمجاز، پروتکل‌های ناامن، پورت‌های غیررسمی و تلاش‌های SSRF.
2. **عملیات اتمیک فایل:** تضمین عدم رها شدن فایل‌های `.tmp` و حفظ پایداری فایل کانفیگ در قطعی ناگهانی برق.
3. **صحت دکودینگ تصویر و عدم قفل ماندن فایل:** دکود مستقیم، آزاد شدن فوری Handle فایل و کش در حافظه رم.
4. **تطبیق فیدهای بین‌المللی:** پارس کردن فیدهای NASA APOD, Wikimedia POTD, ESA Hubble, Picsum/Unsplash, USGS.
5. **مسیریابی هوشمند و تفکیک دسکتاپ و لاک‌اسکرین:** پشتیبانی از حالت‌های مستقل، همزمان و تفکیک مناطق جغرافیایی.
6. **محیط کاملاً آفلاین کلاینت:** آزمون بدون اینترنت و واکشی مستقیم از Share شبکه.
7. **طراحی بصری و فونت فارسی:** تایید بارگذاری قلم فارسی Vazirmatn، جهت‌بندی RTL و آیکون اختصاصی سهند نما.
