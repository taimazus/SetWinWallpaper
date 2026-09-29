# مستندات منابع داده، APIها و الگوریتم یکتاسازی (Sources & API Documentation)

**نرم‌افزار سهند نما (Sahand Nama)**  
**نسخه:** 1.4.0  
**توسعه‌دهنده:** شرکت راهکار الکترونیک سهند ([https://irres.ir](https://irres.ir))

---

## ۱. فهرست و مشخصات منابع آنلاین معتبر

نرم‌افزار سهند نما مستقیماً و بدون وابستگی به API Key پولی، از معتبرترین فیدها و APIهای رسمی استفاده می‌کند:

| شناسه منبع (`Id`) | نام نمایشی فارسی | نوع فرمت داده | هاست مجاز (Allowlisted Host) | حداکثر کیفیت |
| :--- | :--- | :--- | :--- | :--- |
| `Bing` | تصویر روز مایکروسافت بینگ | JSON API | `www.bing.com` | UHD (4K) |
| `BingGlobal` | گلچین بین‌المللی بینگ (همه مناطق) | Multi-JSON | `www.bing.com` | UHD (4K) |
| `UnsplashNature` | عکس‌های برگزیده طبیعت Unsplash | REST / Direct | `picsum.photos`, `images.unsplash.com` | 4K (3840x2160) |
| `WikimediaPotd` | تصویر برگزیده روز ویکی‌مدیا | MediaWiki Action API | `commons.wikimedia.org`, `upload.wikimedia.org` | Full Original |
| `UsgsEarthArt` | شگفتی‌های زمین از فضا (USGS) | RSS / XML Feed | `www.usgs.gov` | High-Res GeoTIFF/JPG |
| `NasaApod` | تصویر نجومی روز ناسا (APOD) | RSS / JSON | `apod.nasa.gov`, `images-assets.nasa.gov` | Ultra HD |
| `EsaHubble` | تصاویر تلسکوپ هابل و وب (ESA) | RSS 2.0 Feed | `esahubble.org` | 4K / Full Res |
| `SharedNetwork` | مخزن اشتراکی سرور در شبکه | UNC SMB | `\\<SERVER>\<SHARE>` | کیفیت اصلی |
| `LocalFolder` | پوشه شخصی در رایانه | Local File System | دیسک محلی | کیفیت اصلی |

---

## ۲. دیاگرام جریان دریافت و اعتبارسنجی امنیتی شبکه

```mermaid
flowchart TD
    Req[درخواست دریافت تصویر از منبع] --> SecCheck{اعتبارسنجی امنیتی آدرس<br/>Allowed Host Check}
    
    SecCheck -- آدرس نامعتبر یا غیرمجاز --> Err[رد درخواست و لاگ امنیتی]
    SecCheck -- آدرس معتبر و مجاز --> HttpStream[ارتباط HTTPS امن و استریم فایل]
    
    HttpStream --> SizeCheck{بررسی حجم فایل<br/>حداکثر ۱۵۰ مگابایت}
    SizeCheck -- بیش از حد مجاز --> Err2[قطع دانلود جهت جلوگیری از سرریز حافظه]
    SizeCheck -- مجاز --> HashCalc[محاسبه هش محتوا SHA-256]
    
    HashCalc --> DupCheck{آیا هش در آرشیو موجود است؟}
    DupCheck -- بله تکراری است --> SkipWrite[صرف‌نظر از ذخیره تکراری و استفاده از فایل موجود]
    DupCheck -- خیر تصویر جدید است --> SaveDisk[ذخیره اتمیک در دیسک و ثبت متادیتا]
```

---

## ۳. الگوریتم یکتاسازی محتوا و پاک‌سازی فایل‌های تکراری (SHA-256 Deduplication)

برای اطمینان از اینکه هیچ تصویر تکراری حتی با نام فایل یا URL متفاوت دوباره بر روی دیسک ذخیره نشود:

1. **محاسبه هش محتوا:** پیش از نهایی‌سازی فایل، هش `SHA-256` بایت‌های تصویر محاسبه می‌شود.
2. **بررسی پایگاه داده آرشیو:** اگر فایلی با همان هش در `archive.json` ثبت شده باشد، فرآیند دانلود متوقف شده و فایل موجود استفاده می‌گردد.
3. **متد `PurgeDuplicates`:** با فراخوانی این متد، تمام فایل‌های موجود در پوشه تصاویر اسکن شده، فایل‌های همسان شناسایی شده و فقط نسخه اصلی (Canonical) حفظ و فایل‌های تکراری حذف می‌گردند.

---

## ۴. ساختار متادیتای تصاویر (`metadata.json` / `archive.json`)

```json
[
  {
    "id": "bing-20260924-us",
    "title": "دریاچه زمردین در میان کوه‌های آلپ",
    "caption": "عکاسی شده در ایالت باواریا، آلمان",
    "copyright": "© John Doe / Bing",
    "sourceName": "Bing Daily (US)",
    "sourceUrl": "https://www.bing.com/...",
    "filePath": "C:\\Users\\...\\AppData\\Local\\BingWallpaperPro\\Images\\2026-09-24-Bing.jpg",
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "dateUtc": "2026-09-24T00:00:00Z"
  }
]
```
