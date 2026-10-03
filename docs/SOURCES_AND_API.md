<div dir="rtl" align="right" style="font-family: 'Vazirmatn', Tahoma, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.8;">

# مستندات منابع داده، APIها و الگوریتم یکتاسازی (Sources & API Documentation)

**نرم‌افزار سهند نما (Sahand Nama)**  
**نسخه:** 1.8.1
**توسعه‌دهنده:** شرکت راهکار الکترونیک سهند ([https://irres.ir](https://irres.ir))

---

## ۱. فهرست و مشخصات منابع آنلاین معتبر

نرم‌افزار سهند نما مستقیماً و بدون وابستگی به API Key پولی، از معتبرترین فیدها و APIهای رسمی استفاده می‌کند:

| شناسه منبع (`Id`) | نام نمایشی فارسی | نوع فرمت داده | هاست مجاز (Allowlisted Host) | حداکثر کیفیت |
| :--- | :--- | :--- | :--- | :--- |
| `Bing` | تصویر روز مایکروسافت بینگ | JSON API | `www.bing.com` | UHD (4K) |
| `BingGlobal` | گلچین بین‌المللی بینگ (همه مناطق) | Multi-JSON | `www.bing.com` | UHD (4K) |
| `IranNature` | ایران زیبا (هشت موضوع طبیعت و میراث) | MediaWiki JSON / imageinfo | `commons.wikimedia.org`, `thumb.wikimedia.org`, `upload.wikimedia.org` | thumbnail تا عرض 1920، وابسته به اصل تصویر |
| `Wallhaven` | والپیپرهای برگزیده Wallhaven | Direct CDN | `w.wallhaven.cc`, `th.wallhaven.cc` | حداکثر ضلع 3840 پس از تبدیل |
| `MuseumArt` | نقاشی‌های آزاد موزه شیکاگو در Wikimedia | MediaWiki JSON / imageinfo | `commons.wikimedia.org`, `thumb.wikimedia.org`, `upload.wikimedia.org` | thumbnail تا عرض 1920، وابسته به اصل تصویر |
| `NatGeoNature` | مناظر برگزیدهٔ ویکی‌مدیا (شناسهٔ تاریخی) | Commons JSON search/imageinfo؛ دستهٔ Featured pictures of landscapes | `commons.wikimedia.org`, `upload.wikimedia.org`, `thumb.wikimedia.org` | حداکثر ضلع 3840 پس از تبدیل |
| `CyberpunkArt` | هنر سایبرپانک از Wallhaven | REST / CDN | `wallhaven.cc`, `w.wallhaven.cc` | حداکثر ضلع 3840 پس از تبدیل |
| `Architecture4K` | معماری از Wallhaven | REST / CDN | `wallhaven.cc`, `w.wallhaven.cc` | حداکثر ضلع 3840 پس از تبدیل |
| `UnsplashNature` | عکس‌های برگزیده طبیعت Unsplash | REST / Direct | `picsum.photos`, `images.unsplash.com` | 4K (3840x2160) |
| `WikimediaPotd` | تصویر برگزیده روز ویکی‌مدیا | MediaWiki Action API | `commons.wikimedia.org`, `upload.wikimedia.org` | Full Original |
| `UsgsEarthArt` | شگفتی‌های زمین از فضا (USGS) | RSS / XML Feed | `eros.usgs.gov`, `landsat.usgs.gov` | High-Res Satellite |
| `NasaDaily` | NASA Image of the Day (غیر از APOD) | RSS | `www.nasa.gov`, `images-assets.nasa.gov` | Ultra HD |
| `NasaLibrary` | کتابخانه تصاویر نجومی ناسا | REST API | `images-api.nasa.gov` | Ultra HD |
| `EsaHubble` | تصاویر تلسکوپ هابل و وب (ESA) | RSS 2.0 Feed | `esahubble.org`, `cdn.esahubble.org` | 4K / Full Res |
| `Spotlight` | مایکروسافت اسپات‌لایت (کش محلی) | Local Cache | دیسک سیستم | کیفیت اصلی |
| `SharedNetwork` | مخزن اشتراکی سرور در شبکه | UNC SMB | `\\<SERVER>\<SHARE>` | کیفیت اصلی |
| `Folder` | پوشه شخصی در رایانه | Local File System | دیسک محلی | کیفیت اصلی |
| `Favorites` | تصاویر نشان‌شده در علاقه‌مندی‌ها | Local Archive | دیسک محلی | کیفیت اصلی |

---

## ۲. مستندات وب‌سرویس وضعیت آب‌وهوا (Open-Meteo Integration)

نرم‌افزار برای نمایش وضعیت لحظه‌ای آب‌وهوا در ویجت دسکتاپ از وب‌سرویس عمومی و رایگان **Open-Meteo** استفاده می‌کند:
- **نقطه نهایی (Endpoint):**
  ```
  https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m&timezone=auto
  ```
- **ویژگی‌های معماری:**
  - بدون نیاز به ثبت‌نام یا API Key اختصاصی.
  - نگاشت مختصات جغرافیایی شهرهای ایران و چند شهر خارجی (تهران، تبریز، مشهد، اصفهان، شیراز، اهواز، رشت، کیش و...).
  - سیستم کش هوشمند محلی به مدت ۱۵ دقیقه جهت کاهش بار شبکه.
  - تبدیل خودکار WMO Weather Codes به برچسب‌های فارسی و ایموجی‌های هواشناسی (صاف و آفتابی ☀️، نیمه ابری ⛅، مه 🌫️، بارانی 🌧️، رعد و برق ⛈️، برف ❄️).

---

## ۳. دیاگرام جریان دریافت و اعتبارسنجی امنیتی شبکه

```mermaid
flowchart TD
    Req[درخواست دریافت تصویر از منبع] --> SecCheck{اعتبارسنجی امنیتی آدرس<br/>SourceHttp.Validate}
    
    SecCheck -- پروتکل ناامن یا هاست غیرمجاز --> Err[رد درخواست و لاگ امنیتی]
    SecCheck -- آدرس HTTPS و هاست مجاز --> HttpStream[ارتباط HTTPS امن و استریم فایل]
    
    HttpStream --> SizeCheck{بررسی حجم فایل<br/>حداکثر ۱۵۰ مگابایت}
    SizeCheck -- بیش از حد مجاز --> Err2[قطع دانلود جهت جلوگیری از سرریز حافظه]
    SizeCheck -- مجاز --> HashCalc[ساخت شناسه از URL و بررسی فایل سالم]
    
    HashCalc --> DupCheck{آیا هش در آرشیو موجود است؟}
    DupCheck -- بله تکراری است --> SkipWrite[صرف‌نظر از ذخیره تکراری و استفاده از فایل موجود]
    DupCheck -- خیر تصویر جدید است --> SaveDisk[ذخیره اتمیک در دیسک و ثبت متادیتا]
```

---

## ۴. الگوریتم یکتاسازی محتوا و پاک‌سازی فایل‌های تکراری (SHA-256 Deduplication)

پیش از دانلود، وجود فایل سالم با همان URL یا شناسه بررسی می‌شود. شناسهٔ منابع آنلاین از هش URL ساخته می‌شود؛ این بررسی جایگزین هش محتوا نیست و مانع دانلود دو URL متفاوت با محتوای یکسان نمی‌شود.

متد `PurgeDuplicates` پس از دریافت، فایل‌های JPG هم‌اندازه را با SHA-256 محتوا مقایسه می‌کند، مسیرهای آرشیو را به نسخهٔ canonical تغییر می‌دهد و نسخه‌های تکراری را حذف می‌کند. فایل تصویر فعال در اولویت حفظ قرار دارد. عملیات با دانلود و تغییر آرشیو قفل مشترک دارد.

---

## ۵. ساختار متادیتای تصاویر (`archive.json`)

```json
[
  {
    "Id": "9b1a5e78c9a3",
    "Source": "Bing",
    "SourcePage": "https://www.bing.com/...",
    "Title": "دامنه‌های کوهستان سهند و دره باستانی کندوان",
    "Copyright": "طبیعت آذربایجان شرقی • رشته‌کوه سهند",
    "Date": "20261002",
    "Url": "https://www.bing.com/th?id=example",
    "Market": "en-US",
    "FilePath": "C:\\Users\\...\\AppData\\Local\\BingWallpaperPro\\Images\\9b1a5e78c9a3.jpg"
  }
]
```

</div>


منابع ایران و موزه از `generator=search` و `prop=imageinfo` نشانی واقعی thumbnail، نام هنرمند و مجوز را می‌گیرند؛ مسیر فایل و hash CDN به‌صورت حدسی ساخته نمی‌شود. موضوعات ایران ثابت‌اند ولی فایل منتخب به نتایج جست‌وجوی Commons وابسته است. نقاشی‌های موزه از دستهٔ `Paintings in the Art Institute of Chicago` می‌آیند. مسیر IIIF مستقیم Art Institute در شبکهٔ آزمون 403 با Cf-Mitigated=challenge داد؛ برنامه این چالش را دور نمی‌زند و از نسخه‌های آزاد مستقل Commons استفاده می‌کند.
