# گزارش ممیزی و حلقهٔ اصلاح سهند نما

تاریخ: 2026-10-03 — اجرای `enterprise-audit` و سپس `audit-fix-loop` روی working tree همین مخزن. این گزارش جایگزین ادعای تاریخی «صفر نقص» شد؛ نسخهٔ ورودی در `artifacts/audit-report-before-fix-loop.md` محفوظ است. شماره‌های خط زیر متعلق به نسخهٔ اصلاح‌شده‌اند و محل قرارداد/اصلاح را نشان می‌دهند؛ سناریوی شکست ستون بعد مربوط به نسخهٔ پیش از اصلاح است.

## نتیجه و قرارداد شواهد

۳۶ یافتهٔ تأییدشده (D01–D14 و N01–N22) اصلاح شدند؛ یافتهٔ تأییدشدهٔ باز یا مسدود: صفر. پنج چرخهٔ اجرای اولیه ثبت شد؛ چرخهٔ پنجم در snapshot همان اجرا یافتهٔ actionable جدید نداشت. اصلاحات هدفمند بعدی N17–N22 بر اساس گزارش‌های runtime کاربر ثبت شدند؛ این مراحل جای ممیزی تازهٔ کل مخزن را نمی‌گیرند. این گزاره تضمین نبود همهٔ باگ‌ها یا تأیید production نیست. ریسک‌ها و فرضیه‌ها در جدول جدا باقی مانده‌اند.

- **E1: مشاهده/بازتولید اجرایی**؛ fixture محلی، mock حمل‌ونقل یا output تصویری. شواهد mock اثبات یکپارچگی سرویس واقعی نیست.
- **E2: نقص اثبات‌شده از مسیر کد/قرارداد**؛ trigger و caller قابل نشان دادن است، ولی لزوماً سناریوی کامل روی میزبان اجرا نشده. موفقیت build به‌تنهایی اثبات صحت runtime نیست.
- **R: ریسک**؛ مسیر خطر دیده شده ولی پیامد موردنظر بازتولید نشده. **H: فرضیه**؛ برای حکم قطعی شواهد بیشتری لازم است.
- شدت High برای حذف دادهٔ خارج مالکیت؛ Medium برای شکست قرارداد عملیاتی/داده/امنیت؛ Low برای رفتار محدود، نمایش یا عمر منابع. یافته‌های مربوط به علت مشترک ادغام شده‌اند؛ توسعهٔ D02/D04/N09 دوباره شمرده نشده است.

## هدف و stack استنباط‌شده از کد

سهند نما برنامهٔ دسکتاپ فارسی برای دریافت/آرشیو تصاویر، انتخاب مستقل desktop و lock screen، Favorites و گردش پوشه/UNC است؛ widget ساعت/هوا/سخت‌افزار، tray و hotkey هم دارد. حالت سازمانی شامل downloader در SCM و task تعاملی هر کاربر است. از C#، WPF/XAML، `net10.0-windows`، JSON، HttpClient، XML/RSS، Win32/COM/WinRT و Windows PowerShell 5.1 استفاده می‌کند. پروژه PackageReference صریح ندارد. نسخهٔ اسمبلی 1.8.0 و خروجی هدف single-file/self-contained win-x64 است. دادهٔ UI در `%LOCALAPPDATA%/BingWallpaperPro` و feed سرویس در `%ProgramData%/BingWallpaperPro/Feed` نگهداری می‌شود؛ اشتراک ساخته‌شده از UI به root همان کاربر اشاره می‌کند.

## فهرست واحد نقص‌های تأییدشده

| شناسه | شدت | طبقهٔ شواهد | path:line فعلی | سناریوی شکست پیش از اصلاح | تغییر | تست یا تأیید و محدودیت آن | وضعیت |
|---|---|---|---|---|---|---|---|
| D01 | High | E1 | `SahandNama/Diagnostics.cs:278` | RepairArchive فایل غیرتصویری بیرون Images را از مسیر metadata حذف می‌کرد. | حذف صرفاً رکورد؛ پاک‌سازی فایل فقط با مالکیت مسیر. | فایل شاهد بیرونی حفظ می‌شود؛ `SahandNama.Tests/AuditRegressionTests.cs:23` | رفع‌شده |
| D02 | Medium | E2 + E1 قرارداد | `SahandNama/WindowsIntegration.cs:154` | نام قدیمی executable در CMD/سرویس؛ نسخهٔ تغییرنام‌یافته کنار exe قدیمی می‌توانست مسیر اشتباه بگیرد و repair زمان‌بندی را رد کند. | helper واحد embedded، exe جاری مقدم، حساب LocalService، بررسی کدهای SCM؛ مسیر ابزار آیکون اصلاح شد. | تست exe مجاور قدیمی `SahandNama.Tests/AuditRegressionTests.cs:65`؛ چهار بررسی installer/CMD در ScriptContractTests؛ نصب زنده اجرا نشده. | رفع‌شده |
| D03 | Medium | E1 | `SahandNama/Sources.cs:47` | Desktop Daily با Lock Random تصادفی می‌شد؛ Follow دو انتخاب مستقل می‌کرد. | حالت‌های مستقل؛ انتخاب desktop در session کش می‌شود؛ Previous نسبت به همان انتخاب. | ۱۰۰ انتخاب Follow یکسان و یک fetch؛ `SahandNama.Tests/AuditRegressionTests.cs:75` | رفع‌شده |
| D04 | Medium | E1 / E2 مسیر backup | `SahandNama/Diagnostics.cs:237` | آرشیو خراب به [] تبدیل می‌شد؛ تنظیمات خراب نیز بعد از شکست backup قابل بازنویسی بود. | نسخهٔ اصلی با نام GUID پیش از جایگزینی کپی می‌شود؛ شکست backup یا I/O عبور می‌کند. | بایت‌های خراب archive/settings در backup باقی می‌ماند؛ `SahandNama.Tests/AuditRegressionTests.cs:27` | رفع‌شده |
| D05 | Medium | E1 | `SahandNama/Core.cs:101` | snapshot قدیمی پنجره، Favorites/مختصات/تنظیم جدید ویجت را بازنویسی می‌کرد. | settings.lock و patch تغییرات نسبت به baseline؛ نویسنده‌ها فقط فیلد هدف را تغییر می‌دهند. | favorite، مختصات و pin جدید بعد از ذخیرهٔ editor حفظ می‌شوند؛ `SahandNama.Tests/AuditRegressionTests.cs:58` | رفع‌شده |
| D06 | Medium | E2 + E1 قرارداد | `SahandNama/Core.cs:116` | اول آرشیو به عنوان تصویر فعال استفاده می‌شد؛ hotkey گالری منتخب را ستاره‌دار می‌کرد. | ثبت آخرین apply موفق و مصرف همین شناسه در tray/widget/hotkey/accent؛ restore metadata را حذف می‌کند. | round-trip مستقل از آرشیو؛ `SahandNama.Tests/AuditRegressionTests.cs:66`؛ تغییر wallpaper توسط برنامهٔ دیگر در پوشش نیست. | رفع‌شده |
| D07 | Medium | E2 تنظیم CI | `.github/workflows/build-and-release.yml:53` | push شاخه main می‌توانست release main را latest کند یا assets را clobber کند. | انتشار فقط refs/tags/v؛ build/artifact شاخه برقرار است. | ScriptContractTests قید tag را تأیید می‌کند؛ workflow روی GitHub اجرا نشده. | رفع‌شده |
| D08 | Medium | E1 binding + E2 | `SahandNama/WindowsIntegration.cs:316` | Set-SmbShare -Path پارامتر معتبر ندارد؛ تغییر مسیر share شکست می‌خورد. | تعارض مسیر با خطای روشن رد می‌شود؛ share موجود خودکار حذف نمی‌شود. | binding محلی -WhatIf شکست قبل را نشان داد؛ بازبینی حذف فرمان نامعتبر؛ SMB واقعی تغییر نکرد. | رفع‌شده |
| D09 | Medium | E1 | `SahandNama/Sources.cs:408` | همگام‌سازی همه فقط بخشی از منابع را فراخوانی می‌کرد؛ شکست کامل با cache موفق گزارش می‌شد. | ۱۴ منبع آنلاین از Options؛ allowOffline=false؛ شکست تجمیعی، دادهٔ موفق حفظ و cancellation منتقل می‌شود. | HTTP 503 همهٔ منابع با cache موجود خطا می‌دهد؛ `SahandNama.Tests/AuditRegressionTests.cs:130` | رفع‌شده |
| D10 | Medium | E2 + E1 helper | `SahandNama/Core.cs:81` | Main خطای JSON را می‌گرفت ولی App/Widget دوباره بدون محافظ می‌خواندند و startup شکست می‌خورد. | خواندن startup با پیش‌فرض و log؛ فایل خراب حفظ می‌شود. | JSON خراب حفظ و defaults خوانده می‌شود؛ `SahandNama.Tests/AuditRegressionTests.cs:60`؛ راه‌اندازی کامل UI آزموده نشده. | رفع‌شده |
| D11 | Medium | E1 | `SahandNama/Sources.cs:102` | timeout نوع OperationCanceled در metadata fallback را دور می‌زد. | timeout حمل‌ونقل recoverable؛ لغو صریح caller منتقل؛ deadline در کل stream. | transport timeout برای Bing/NASA cache هم‌منبع می‌دهد؛ cancellation صریح رد می‌شود؛ `SahandNama.Tests/AuditRegressionTests.cs:122` | رفع‌شده |
| D12 | Medium | E2 | `SahandNama/TrayManager.cs:218` | --widget آیکون tray می‌ساخت ولی HWND ویجت hook پیام tray نداشت. | Tray مالک hook روی HWND اولیه؛ hook پنجرهٔ اصلی دیگر دوباره پیام را مصرف نمی‌کند. | بازبینی مسیر App --widget/Initialize/MessageHook و build؛ کلیک واقعی tray اجرا نشده. | رفع‌شده |
| D13 | Medium | E2 قرارداد docs | `docs/ENTERPRISE_DEPLOYMENT.md:69` | نمونهٔ SharedNetwork فقط DesktopFolder/LockFolder داشت و Request مسیر شبکهٔ خالی می‌گرفت. | نمونه‌ها از NetworkSharePath و LockNetworkSharePath استفاده می‌کنند؛ تفاوت root سرویس و UI توضیح داده شد. | بررسی ایستای سه نمونهٔ deployment/admin و Request؛ اتصال UNC واقعی آزموده نشده. | رفع‌شده |
| D14 | Low | E1 | `SahandNama/Core.cs:171` | pt-BR / it-IT / es-ES در BingGlobal بودند ولی validator ردشان می‌کرد. | مجموعهٔ validator با مناطق catalog هماهنگ شد. | هر سه market پذیرفته می‌شوند؛ `SahandNama.Tests/AuditRegressionTests.cs:132` | رفع‌شده |
| N01 | Medium | E2 | `SahandNama/App.xaml.cs:237` | در اجرای widget-only، WPF همان ویجت را MainWindow می‌کرد و check null پنجرهٔ واقعی را نمی‌ساخت. | نوع MainWindow هم بررسی می‌شود؛ ایجاد تکراری widget هنگام startup جلوگیری شد. | بازبینی lifecycle و build؛ اجرای UI مستقل/کلیک واقعی محدودیت پوشش است. | رفع‌شده |
| N02 | Medium | E1 | `SahandNama/Sources.cs:295` | fallback منبع نامرتبط و Folder خالی را با تصاویر آنلاین آرشیو جایگزین می‌کرد. | فقط فایل سالم هم‌منبع؛ Folder fallback ندارد. | MuseumArt با cache NASA و Folder خالی هر دو خطا؛ `SahandNama.Tests/AuditRegressionTests.cs:126` | رفع‌شده |
| N03 | Medium | E1 | `SahandNama/Sources.cs:454` | FilePath مطلق آرشیو share می‌توانست فایل محلی خارج share را وارد کند. | basename در root/Images share؛ کنترل reparse/مالکیت و decode. | آرشیو share با FilePath بیرونی رد می‌شود؛ `SahandNama.Tests/AuditRegressionTests.cs:135` | رفع‌شده |
| N04 | Medium | E1 | `SahandNama/Sources.cs:493` | قطع File.Copy مستقیم، تصویر سالم مقصد را با بایت‌های ناقص جایگزین می‌کرد. | temp یکتا، decode، cancellation check، حفظ mtime، Move و cleanup. | copy تزریق‌شده پس از نوشتن partial خطا می‌دهد؛ مقصد قبلی یکسان و temp پاک؛ `SahandNama.Tests/AuditRegressionTests.cs:144` | رفع‌شده |
| N05 | Medium | E1 | `SahandNama/Scripts/Manage-Service.ps1:2` | PowerShell 5.1 اسکریپت فارسی UTF-8 بدون BOM را با encoding نامناسب می‌خواند و parser شکست می‌خورد. | UTF-8 BOM برای PS1های مربوط؛ helper embedded همان منبع را مصرف می‌کند. | شکست parser پیش از اصلاح؛ پنج ParseFile روی Windows PowerShell 5.1 اکنون PASS. | رفع‌شده |
| N06 | Medium | E1 | `SahandNama/Diagnostics.cs:213` | archive=null، رکورد null و Favorites=null تعمیر را متوقف می‌کردند. | backup برای JSON null، حذف رکورد null، normalize Favorites. | سه fixture مستقل null؛ `SahandNama.Tests/AuditRegressionTests.cs:82` | رفع‌شده |
| N07 | Medium | E1 + E2 callers | `SahandNama/Diagnostics.cs:31` | خطا داخل RepairedItems و --repair با exit 0 گزارش می‌شد. | Errors/ErrorSummary مجزا؛ UI خطا و CLI exit 1؛ موفقیت جزئی حفظ می‌شود. | Images به‌صورت فایل مانع ساخت پوشه؛ نتیجه Errors دارد؛ `SahandNama.Tests/AuditRegressionTests.cs:90`؛ exit/UI از کد بررسی شد، repair سیستم اجرا نشده. | رفع‌شده |
| N08 | Medium | E1 | `SahandNama/Sources.cs:851` | purge می‌توانست فایل تصویر فعال را به عنوان duplicate حذف کند و metadata جاری نامعتبر شود. | تصویر فعال canonical اولویت دارد؛ ارجاع‌های آرشیو منتقل می‌شوند. | دو فایل یکسان؛ active b باقی می‌ماند و a حذف می‌شود؛ `SahandNama.Tests/AuditRegressionTests.cs:142` | رفع‌شده |
| N09 | Low | E2 قرارداد | `docs/SOURCES_AND_API.md:28` | docs/UI منبع NASA را APOD، Wikimedia را NatGeo و خروجی محدودشده را 8K معرفی می‌کردند؛ repair با reset Spotlight یکی دانسته می‌شد. | تطبیق README/Guide/wiki/XAML با IOTD، host و کیفیت واقعی؛ رفع لینک و شرح CLI. | بازبینی endpoints/parser/ConvertToJpeg و بررسی ایستای نمونه‌ها؛ اعتبار آنلاین/حقوق محتوا ارزیابی نشد. | رفع‌شده |
| N10 | Medium | E1 | `SahandNama/Core.cs:154` | AcquireLockAsync با caller سنکرون UI و قفل متنازع، context UI را برای ادامه لازم داشت و deadlock می‌کرد. | ConfigureAwait(false) در backoff و لغو پیش از تلاش. | SynchronizationContext بدون pump همراه آزادشدن قفل تکمیل می‌شود؛ `SahandNama.Tests/AuditRegressionTests.cs:46` | رفع‌شده |
| N11 | Low | E1 | `SahandNama/DesktopWidgetWindow.xaml.cs:493` | Tag=0.80 با culture مثل fr-FR parse نمی‌شد. | InvariantCulture برای مقدار ثابت menu. | fr-FR مقدار 0.8؛ `SahandNama.Tests/AuditRegressionTests.cs:202` | رفع‌شده |
| N12 | Low | E2 | `SahandNama/DesktopWidgetWindow.xaml.cs:246` | DispatcherTimerهای بسته‌شده ویجت را نگه می‌داشتند و Tick ادامه داشت. | Stop هر سه timer در OnClosed. | بازبینی reference/lifecycle و build؛ profiling حافظه/بستن واقعی UI انجام نشد. | رفع‌شده |
| N13 | Low | E2 | `SahandNama/TrayManager.cs:82` | stream منبع icon در Initialize Dispose نمی‌شد. | using منبع stream و Icon. | بازبینی lifetime و build؛ شمارش handle در اجرای بلندمدت انجام نشد. | رفع‌شده |
| N14 | Medium | E1 تصویری | `SahandNama/Presentation.cs:170` | عنوان و اعتبار فارسی به‌علت origin خارج کادر متن، بیرون کارت رسم می‌شدند. | origin داخل MaxTextWidth؛ title و credit هر دو اصلاح. | pixeldiff قبل FAIL، بعد PASS؛ مشاهدهٔ PNG عنوان/اعتبار داخل کادر؛ `SahandNama.Tests/AuditRegressionTests.cs:209` | رفع‌شده |
| N15 | Medium | E2 + E1 helper | `SahandNama/WindowsIntegration.cs:565` | ImportSpotlight مستقیم File.Create نهایی می‌نوشت؛ شکست encoder فایل ناقص می‌گذاشت و وجود آن در دور بعد کافی بود. | تبدیل atomic مشترک؛ decode فایل موجود قبل از reuse. | ورودی نامعتبر مقصد سالم را عوض نمی‌کند و temp باقی نمی‌گذارد؛ `SahandNama.Tests/AuditRegressionTests.cs:149`؛ کش واقعی Spotlight دست‌کاری نشد. | رفع‌شده |
| N16 | Medium | E2 + E1 filesystem | `SahandNama/WindowsIntegration.cs:452` | ResetSpotlight خطای backup/delete را می‌بلعید و AppX با SilentlyContinue موفق گزارش می‌شد. | جمع‌آوری خطا، حفظ originals در backup failure، backup GUID؛ AppX Stop و رد نبود بسته؛ شکست جزئی exception/log. helper پاک‌سازی قدیمی نیز خطا را منتقل می‌کند. | backup غیرقابل نوشتن خطا و فایل اصلی سالم؛ backup موفق حفظ بایت پیش از حذف؛ `SahandNama.Tests/AuditRegressionTests.cs:153`؛ Registry/AppX واقعی اجرا نشده. | رفع‌شده |

| N17 | Medium | E1 تصویر کاربر + بازتولید Windows PowerShell 5.1 | `SahandNama/WindowsIntegration.cs:102`؛ `SahandNama/Diagnostics.cs:137` | آرایهٔ System.Object[] به overload BindingFlags متصل می‌شد و GetFileFromPathAsync پیش از تغییر لاک‌اسکرین شکست می‌خورد؛ IsSupported هم اشتباهاً property فرض شده بود. | Type[] صریح در سه lookup؛ فراخوانی متد بدون آرگومان IsSupported در apply و diagnostics؛ script واقعی برای تست قابل استخراج شد. | `SahandNama.Tests/AuditRegressionTests.cs:194`؛ خطای قبلی با BindingFlags بازتولید و script اصلاح‌شده StorageFile را باز می‌کند و متدهای personalization/fallback را می‌یابد؛ هیچ setter اجرا نشده. | رفع‌شده |

| N18 | Medium | E1 cache timeout + E2 exit مسیر scheduled | `SahandNama/Core.cs:328`؛ `SahandNama/WindowsIntegration.cs:252` | دریافت ناموفق با cache به exit 0 تبدیل می‌شد و Task Scheduler تلاش مجدد نمی‌کرد؛ retry پیشین هم سه بار پنج‌دقیقه‌ای بود. | fallback/دانلود جزئی به catalog منتقل می‌شود؛ فقط اجرای scheduled پس از استفاده از نتیجهٔ ناقص exit 1 می‌دهد؛ ۲۴ retry ساعتی، سرویس failure یک ساعت. | `SahandNama.Tests/AuditRegressionTests.cs:165`؛ HTTP timeout، نشانگر fallback Bing/catalog و ساخت actual Task objects بدون ثبت task تأیید شد. | رفع‌شده |

| N19 | Medium | E1 لاگ کاربر و HTTP زنده | `SahandNama/Sources.cs:337` | هشت URL ثابت ایران همگی 404 می‌دادند؛ مسیرهای filename/hash حدسی بودند. | حل تصویر از MediaWiki imageinfo، هشت موضوع جست‌وجو و اعتبار Artist/License واقعی؛ حذف URLهای ثابت. | `SahandNama.Tests/AuditRegressionTests.cs:177`؛ mock pipeline و دریافت واقعی ۸ تصویر سالم در artifacts. | رفع‌شده |
| N20 | Low | E1 پیام و fixture HTTP | `SahandNama/Sources.cs:234` | پیام کلی «ارتباط برقرار نشد» علت واقعی 403/503 یا خوراک خالی را از UI حذف می‌کرد و به‌صورت خطای لاک‌اسکرین دیده می‌شد. | آخرین علت HTTP/parser در exception قابل نمایش حفظ می‌شود؛ نبود تصاویر با قطع اینترنت یکی دانسته نمی‌شود. | `SahandNama.Tests/AuditRegressionTests.cs:190`؛ پیام UI برای HTTP 503 علت را نگه می‌دارد. | رفع‌شده |

| N21 | Medium | E1 regression قبل FAIL / بعد PASS | `SahandNama/Sources.cs:763` | reuse فایل هم‌URL از منبع دیگر، Id همان رکورد را هم کپی می‌کرد؛ merge منبع دوم رکورد منبع اول را بازنویسی می‌کرد و cache آفلاین آن از دست می‌رفت. | فقط bytes/FilePath مشترک می‌ماند؛ شناسهٔ مستقل Source+URL حفظ می‌شود. | `SahandNama.Tests/AuditRegressionTests.cs:188`؛ fixture تصویر واقعی بزرگ‌تر از ۱۰۰۰ بایت، دو منبع و فایل مشترک؛ هر دو رکورد پس از merge باقی‌اند. | رفع‌شده |
| N22 | Medium | E1 پاسخ HTTP واقعی + regression قبل FAIL / بعد PASS | `SahandNama/Sources.cs:196`؛ parser `:267` | منبع NatGeoNature از featuredfeed با feed=featured نامعتبر، HTML با HTTP 200 می‌گرفت؛ ParseWikimediaFeed با mismatched tag شکست می‌خورد و بدون cache لاک‌اسکرین به‌روز نمی‌شد. | Commons JSON search/imageinfo برای دستهٔ مناظر برگزیده؛ parser metadata واقعی؛ نام نمایشی مطابق مجموعه. | `SahandNama.Tests/AuditRegressionTests.cs:185`؛ تست قرارداد و pipeline، سپس ۱۵ دانلود واقعی سالم بدون fallback؛ `artifacts/featured-live.log`. | رفع‌شده |

## ریسک‌ها و فرضیه‌ها؛ خارج از شمار نقص‌های تأییدشده

| شناسه | شدت / طبقه | شاهد | سناریو و وضعیت | تأیید لازم / انجام‌شده |
|---|---|---|---|---|
| R01 | Medium / R | `SahandNama/Sources.cs:103` | auto-redirect امکان عبور از host allowlist داشت؛ exploit واقعی ثبت نشده. کنترل دفاعی اضافه شد: validate هر hop، حداکثر پنج redirect، رد credentials/port/scheme. | mock مقصد ممنوع اصلاً درخواست دریافت نمی‌کند؛ redirect مجاز و loop آزموده شد. DNS/شبکهٔ production پوشش ندارد. |
| R02 | Medium / R | `SahandNama/Core.cs:110`؛ `SahandNama/Diagnostics.cs:250` | برخی نویسنده‌ها read-modify-write مشترک نداشتند؛ feed/archive/settings gate و ترتیب feed سپس archive همسان شد. | ۲۰ merge موازی بدون lost update و repair منتظر feed؛ رقابت چند میزبان روی UNC/قطع برق آزموده نشده. |
| R03 | Medium / R باز | `SahandNama/Sources.cs:806`؛ `SahandNama/Core.cs:110` | decode کامل پیش از scale و نگهداری آرشیو بدون سیاست retention می‌تواند RAM/دیسک را تحت فشار بگذارد؛ OOM یا سقف عملکرد در این اجرا اثبات نشده. | benchmark تصاویر عظیم، archive بزرگ، بودجهٔ RAM/دیسک و سیاست retention لازم است. از تعداد تست‌ها پوشش کارایی استنباط نمی‌شود. |
| H01 | Medium / H | `SahandNama/WindowsIntegration.cs:77` | fallback حدسی Registry/cache حذف شد تا موفقیت ساختگی گزارش نشود؛ WinRT acceptance هنوز اثبات نمایش واقعی روی همهٔ editionها نیست. | بررسی Win+L و GPO/MDM روی Windows 10/11/Server Desktop Experience؛ این اجرا آن را انجام نداد. |

دو ابهام ثانویه در مرور نهایی به‌عنوان نقص اعلام نشدند: تغییر تصویر توسط نرم‌افزار دیگر می‌تواند metadata «آخرین تصویر اعمال‌شده از این برنامه» را قدیمی کند؛ پاسخ دیررس هوا هنگام تعویض شهر ممکن است نمایش موقت شهر قبلی ایجاد کند. تست یکپارچه/latency برای اثبات پیامد اجرا نشده است.

## بازبینی گزارش ورودی و جلوگیری از دوباره‌شماری

| شناسهٔ گزارش ورودی | حکم این اجرا |
|---|---|
| DEF-01 | حل قبلی کافی نبود؛ release روی main در D07 باز شد و اصلاح شد. |
| DEF-02 | reset رویداد/CTS در ServiceEntry موجود و حفظ شد؛ restart واقعی SCM آزموده نشد. |
| DEF-03 | retry موجود بود؛ دامنهٔ قفل در R02 و deadlock context در N10 تکمیل شد. |
| DEF-04 | length + mtime موجود و حفظ شد؛ نوشتن atomic در N04 تکمیل شد. |
| DEF-05 | check null قبلی کافی نبود؛ نوع پنجره N01 و پیام tray D12 اصلاح شد. |
| DEF-06 | workflow نام جدید داشت؛ CMD/helper/runtime و ابزار icon در D02 تکمیل شدند. |
| DEF-07 | UA فعلی 1.8.0 است؛ عدد 1.7.0 گزارش قدیمی قابل اتکا نبود. |
| DEF-08 | ادعای حذف تمام silent failures درست نبود. N07/N16 مسیرهای موفقیت ساختگی را اصلاح کردند؛ catchهای best-effort مانند log/preview باقی‌اند و علت‌شان بررسی شد. |
| SEC-01 | Pack URI جدید قبلاً درست بود؛ تست resolve Theme.xaml موفق ماند؛ یافتهٔ امنیتی تازه از این نام‌گذاری استنباط نشد. |

سه چرخهٔ تاریخی گزارش قبلی شواهد اجرایی این جلسه محسوب نمی‌شوند و به پنج چرخهٔ فعلی اضافه نشده‌اند.

## چرخه‌های این اجرا

| چرخه | اصلاح و بازبینی تازه | شاهد checks |
|---|---|---|
| ۱ | بازبینی ادعاهای ورودی؛ D01–D14، کنترل redirect و قفل‌ها، تست هدفمند و بررسی callerها. | baseline ۷۵؛ مراحل اصلاح ۸۴، ۸۷ و ۱۰۱ check پاس. خطای compile نوع MainWindow و assertion filter اصلاح شد. `artifacts/cycle-1-network.log` |
| ۲ | بررسی تازهٔ UI، shared copy، null/repair، docs و scripts؛ N01–N13. | مراحل ۱۰۶، ۱۰۸ و ۱۱۱ check پاس؛ PS parser شکست encoding را پیش از BOM نشان داد؛ سپس ۱۲ قرارداد script پاس. `artifacts/cycle-2-*.log` |
| ۳ | بازبینی تصویر خروجی؛ N14 و تکمیل D02/D04/N09. | pixeldiff قبل اصلاح FAIL (`cycle-3-card-before.log`)، بعد ۱۱۲ PASS؛ تکمیل مسیر exe و settings backup: ۱۱۴ PASS (`cycle-3-extended.log`). PNG نهایی عنوان/اعتبار داخل کارت دارد. |
| ۴ | مرور مستقل ادامهٔ WindowsIntegration؛ N15/N16 و بازبینی مسیرهای خطا/cleanup. | ۱۱۷ check پاس (`cycle-4-spotlight.log`)؛ backup failure دادهٔ اصلی را حفظ می‌کند و conversion نامعتبر مقصد سالم را تغییر نمی‌دهد. Registry/AppX اجرا نشده. |
| ۵ | مرور تازهٔ تمام source/config/docs، XAML binding/handlers، CLI، service/task، منابع، جریان ذخیره/حذف، workflow و assertions؛ تطبیق diff نهایی. | یافتهٔ actionable جدید در پوشش بررسی‌شده: صفر. build، تست C#، PS contracts، publish و diff checks نهایی در جدول بعد. |

## checks نهایی و محل تغییرها

| check | دستور / نتیجه | مدرک |
|---|---|---|
| C# regression | `dotnet run --project SahandNama.Tests/SahandNama.Tests.csproj --no-restore`؛ ۱۱۷ PASS، exit 0 | `artifacts/final-regression.log`؛ ۷۵ baseline + ۴۲ assertion اضافه، نه ۱۱۷ سناریوی end-to-end |
| PowerShell contracts | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File SahandNama.Tests/ScriptContractTests.ps1`؛ ۱۲ PASS، exit 0 | `artifacts/final-script-checks.log`؛ SCM/ACL/transcript mock و فایل‌ها در temp داخل artifacts |
| Build | `dotnet build SahandNama.slnx --no-restore`؛ موفق، صفر error، یک هشدار NU1900 | `artifacts/final-build.log`؛ هر هشدار NU1900 در خروجی نهایی مربوط به vulnerability feed است |
| Publish | Release، win-x64، self-contained، PublishSingleFile و IncludeNativeLibrariesForSelfExtract؛ موفق، exit 0 | `artifacts/final-publish.log`؛ executable در `artifacts/audit-publish/SahandNama.exe`؛ NU1900 به‌سبب دسترسی‌نداشتن به api.nuget.org |
| Source/docs contract | خواندن UTF-8 تمام ۵۴ فایل متنی tracked؛ بررسی سه نمونهٔ UNC و نبود APOD اشتباه در Guide/XAML | ۶۱ فایل tracked؛ ۷ asset دودویی با بررسی مصرف/بسته‌بندی، نه ممیزی داخلی باینری؛ دو فایل جدید آزمون و گزارش به‌روزشده نیز بررسی شدند |
| Diff | `git diff --check`؛ PASS | تغییر کاربر از قبل در wrapper PowerShell داخل WindowsIntegration حفظ شد؛ commit یا انتشار بیرونی انجام نشد |

تغییرهای داده/انتخاب/دانلود در `Core.cs`، `Sources.cs` و `Diagnostics.cs`؛ اتصال Windows و embedded helper در `WindowsIntegration.cs`، `App.xaml.cs`، csproj و Scripts؛ UI/metadata/lifecycle در MainWindow، DesktopWidgetWindow، TrayManager و Presentation؛ CI در `.github/workflows/build-and-release.yml`؛ آزمون‌ها در `SahandNama.Tests/AuditRegressionTests.cs` و `ScriptContractTests.ps1` و فراخوانی در Program؛ مستندات در README، Guideهای HTML، docs و هر دو wiki. خط‌های دقیق هر اصلاح در فهرست بالا آمده است.

## محدودیت پوشش و کار باقی‌ماندهٔ محیطی

پوشش مرور، کل repository و تغییرهای محلی بود؛ dependency/runtime/.git internals و فایل‌های تولیدی bin/obj به‌عنوان source audit نشدند. نام/مصرف منابع، مجوز OFL فونت، XAML، csproj، workflow، اسکریپت‌ها، CLI و دو مجموعه wiki بررسی شدند. درصد line/branch coverage اندازه‌گیری نشد. تست parser با fixture مصنوعی، HTTP با handler تزریقی و فایل‌سیستم با مسیرهای ایزوله انجام شد؛ upstreamهای زنده، تمام URLهای curated ایران، DNS، proxy و redirectهای واقعی تأیید نشده‌اند.

هیچ wallpaper، policy، سرویس، task، SMB share یا AppX واقعی برای تست تغییر نکرد. تست نمونهٔ report/repair در root ایزوله بود؛ بررسی read-only monitor/registry/API به معنی استقرار enterprise نیست. UAC، حساب LocalService واقعی، ACL inheritance در نصب زنده، domain authentication، GPO/MDM، Win+L، restart Explorer و tray، hotkey/close widget واقعی، DPI و چند نمایشگر با مختصات منفی، shutdown/power loss و performance طولانی‌مدت هنوز نیازمند میزبان آزمایشی‌اند.

vulnerability lookup NuGet با NU1900 به‌علت شبکه کامل نشد؛ publish ادامه یافت ولی نتیجهٔ vulnerability scan نامعلوم است. این مانع کار مستقل را متوقف نکرد. هیچ نقص تأییدشدهٔ حل‌نشده به‌سبب این محدودیت باقی نماند؛ نتیجه برای اعتبارسنجی deployment و ریسک R03/H01 عمداً ناقص است. completion این حلقه فقط رفع یافته‌های تأییدشده و یک pass تازهٔ بدون actionable جدید در همین سطح شواهد است.

## اصلاح پس از گزارش تصویری کاربر — N17

خطای runtime تصویر، محدودیت آزمون قبلی WinRT را آشکار کرد. scope این اصلاح WindowsIntegration، diagnostics و caller لاک‌اسکرین است. بر اساس [مستند رسمی Microsoft](https://learn.microsoft.com/en-us/uwp/api/windows.system.userprofile.userprofilepersonalizationsettings.issupported?view=winrt-26100)، IsSupported یک متد است.

تست رگرسیون با آرایهٔ بدون نوع خطای گزارش‌شدهٔ BindingFlags را بازتولید می‌کند. همان prefix از script تولیدی با Type[] واقعی فایل را باز کرده، async operation را به Task تبدیل می‌کند و signature را پیدا می‌کند. lookup fallback نیز روی WinRT واقعی بررسی شد. مجموعهٔ جدید: ۱۲۰ check C# پاس؛ ۱۲ check PowerShell پاس؛ publish Release win-x64 موفق و diff check پاس. لاگ‌ها: `artifacts/lockscreen-fix-tests.log`، `artifacts/lockscreen-fix-scripts.log` و `artifacts/lockscreen-fix-publish.log`. هشدار NU1900 شبکه همچنان برقرار است.

exe، ZIP و SHA256SUMS در output با بیلد اصلاح‌شده جایگزین شدند؛ نسخهٔ قبلی در artifacts محفوظ است. فایل publish در artifacts/audit-publish مربوط به اجرای اولیه است؛ برای این اصلاح از output استفاده شود. نمایش واقعی تصویر با Win+L و پذیرش policy آزموده نشده؛ نتیجهٔ تست فقط صحت lookup/bridge بدون تغییر پس‌زمینه را اثبات می‌کند.

## درخواست پاک‌سازی و retry ساعتی کاربر

در بررسی خارج sandbox، یک task مربوط به برنامه یافت شد: `BingWallpaperPro-Daily-S-1-5-21-765943429-26906495-2105990965-1000`. XML پیش از حذف در `artifacts/schedule-before-user-reset.xml` ذخیره شد؛ task متوقف، حذف و نبودش تأیید شد. سرویس مربوط به SahandNama/BingWallpaperPro در inventory نصب‌شده نبود. task یا سرویس جدید ثبت نشد؛ کاربر تنظیم مجدد را درخواست کرده است.

سرویس: sync موفق هر ۱۲ ساعت، شکست هر یک ساعت؛ اجرای task: روزانه/ورود، retry شکست هر یک ساعت تا ۲۴ بار، MultipleInstances=IgnoreNew، کاربر Interactive/Limited. حالت `--scheduled` در Action افزوده شد تا موفقیت cache با موفقیت دریافت آنلاین اشتباه نشود. ثبت دوباره از Register-ScheduledTask -Force استفاده می‌کند و پیش از ساخت تنظیمات، task قبلی را حذف نمی‌کند. کد تنظیمات/منطق retry در build جدید output قرار گرفت.

۱۲۵ assertion C# و ۱۲ قرارداد PS پاس شدند؛ تست Task objects برای محدودیت CIM Access denied sandbox خارج sandbox اجرا شد. این تست هیچ task/service/policy/wallpaper ثبت یا اعمال نکرد. سرویس واقعی نصب و start نشد؛ اجرای یک ساعت واقعی و scheduler restart پس از خطای برنامه end-to-end آزموده نشد. لاگ‌ها: `artifacts/hourly-retry-tests.log`، `artifacts/hourly-retry-scripts.log` و `artifacts/hourly-retry-publish.log`. NuGet lookup با محدودیت شبکه NU1900 همچنان نامعلوم است.

پس از build نهایی، ZIP با hash تمام ۱۱ فایل داخل standalone مقایسه شد و exe نسخه‌دار با exe داخل بسته یکسان است؛ SHA256SUMS به‌روز شد. خروجی قبلی در `artifacts/output-before-hourly-retry-4c98f4a54d984085a34431f84d3fdbc4` محفوظ است. دانلود جزئی نیز با fixture دو تصویر (یکی HTTP 503 و دیگری سالم) علامت retry را حفظ می‌کند؛ sync سرویس از موفقیت ناقص عبور نمی‌کند. [مرجع Microsoft برای RestartInterval](https://learn.microsoft.com/en-us/windows/win32/taskschd/tasksettings-restartinterval).

## خطای منابع ایران و موزه — اصلاح هدفمند N19/N20 و ریسک R04

لاگ واقعی کاربر و probe HTTP نشان دادند همهٔ URLهای قبلی ایران 404 هستند. MuseumArt metadata دریافت می‌شد ولی عکس‌های IIIF به 403 می‌رسیدند؛ پاسخ شبکه دارای `Cf-Mitigated: challenge` و صفحهٔ Cloudflare بود. تغییر اندازه به 843 یا اندازهٔ مستند 1686 و میزبان اصلی هم 403 را رفع نکرد. این مانع میزبان ریسک شرطی R04 است و به عنوان نقص اثبات‌شدهٔ کد در شمار بالا اضافه نشده؛ [مستند رسمی Art Institute](https://api.artic.edu/docs/) مسیر IIIF را تأیید می‌کند. هیچ چالش یا محدودیت سایت دور زده نشد.

MuseumArt اکنون نسخه‌های آزاد نقاشی‌های همان مجموعهٔ Art Institute را از Wikimedia Commons دریافت می‌کند؛ دستهٔ `Paintings in the Art Institute of Chicago` و نام نمایشی/مستندات متناسب شدند. API imageinfo نشانی واقعی thumbnail، artist و LicenseShortName را می‌دهد. فایل کوچک، format ناسازگار و میزبان غیرمجاز فیلتر می‌شوند. ایران هشت موضوع جست‌وجو دارد و برای جنگل نتایج map/ecoregion حذف شده‌اند. تغییر مجموعهٔ فایل‌ها در نتایج upstream امکان‌پذیر است؛ تضمین فایل ثابت وجود ندارد.

تأیید واقعی با `dotnet run --project SahandNama.Tests/SahandNama.Tests.csproj --no-build --no-restore -- --verify-sources`: ۸ عکس سالم IranNature و ۱۵ عکس سالم MuseumArt، بدون cache fallback؛ دریافت/تبدیل/آرشیو/decoder واقعی اجرا شد، هیچ wallpaper، service یا task تغییر نکرد. دادهٔ آزمون در `artifacts/live-sources-af8f305e0d464b788e0bfece467eb901` و لاگ در `artifacts/source-fix-live.log` است. probe metadata/thumbnail هر موضوع در `artifacts/verified-commons-source-probes.json` ثبت شد.

۱۳۰ check رگرسیون C# پاس شد؛ build موفق؛ PS contracts ۱۲ پاس؛ publish و hash ZIP/exe خروجی تأیید شدند. لاگ‌ها: `source-fix-tests.log`، `source-fix-build.log`، `source-fix-scripts.log` و `source-fix-publish.log` زیر artifacts. شبکهٔ publisher NuGet همچنان NU1900 داشت. تست نمایش واقعی لاک‌اسکرین و تمام منابع دیگر تکرار نشد؛ این مرحله ممیزی تازهٔ کل repository نیست.

در بازبینی تازهٔ cache، N21 با fixture دو منبع دارای یک URL و JPEG بیش از ۱۰۰۰ بایت بازتولید شد (`artifacts/source-cross-cache-before.log`، exit 1). fixture کوچک قبلی مسیر reuse را تحریک نمی‌کرد. پس از حفظ Id مستقل، مجموعهٔ ۱۳۰ check پاس شد. این اصلاح، اشتراک فایل برای صرفه‌جویی در دانلود را حفظ می‌کند ولی رکوردهای هر source را جدا نگه می‌دارد.

## خطای مناظر برگزیدهٔ Wikimedia — N22

یک چرخهٔ اصلاح هدفمند: probe خوراک قبلی، regression شکست‌خورده، اصلاح، اجرای مجدد همهٔ regressionها و بازبینی مسیر fetch/parser/cache و مصرف‌کنندهٔ sync. HTTP 200 با Content-Type=text/html و خطای XML همان لاگ واقعی مشاهده شد؛ `artifacts/featured-source-probes.json` و `artifacts/featured-before.log` (exit 1). خوراک مستقل WikimediaPotd با HTTP 200 و XML ریشهٔ rss سالم بود. جایگزین JSON از دستهٔ [Featured pictures of landscapes در Commons](https://commons.wikimedia.org/wiki/Category:Featured_pictures_of_landscapes) دریافت می‌شود؛ شناسهٔ تاریخی NatGeoNature برای تنظیمات قبلی محفوظ است.

پس از اصلاح، ۱۳۲ assertion C# و ۱۲ قرارداد PowerShell پاس شدند (exit 0؛ `artifacts/featured-tests.log` و `artifacts/featured-scripts.log`). دستور `dotnet run --project SahandNama.Tests/SahandNama.Tests.csproj --no-build --no-restore -- --verify-featured` با exit 0، ۱۵ تصویر سالم، بدون cache/fallback یا شکست جزئی دریافت کرد؛ فایل‌ها در `artifacts/live-sources-94be0c2e18864120b1828122ad75541e` و لاگ در `artifacts/featured-live.log` هستند. تصویر دسکتاپ/لاک، policy، service یا task در تست تغییر نکردند.

Publish Release win-x64 موفق شد (`artifacts/featured-publish.log`)؛ NU1900 برای دسترسی به vulnerability feed NuGet باقی است و ارزیابی وابستگی‌ها از این مسیر کامل نیست. خروجی تازه در `output` قرار دارد؛ hash تک‌تک فایل‌های ZIP و تطابق exe نسخه‌دار با standalone کنترل شد (`artifacts/featured-package.log`، `output/SHA256SUMS.txt`). خروجی قبلی در backup ذکرشده در لاگ محفوظ است. بازبینی تازهٔ همین مسیر نقص actionable جدیدی نشان نداد؛ کل منابع آنلاین، نمایش واقعی لاک‌اسکرین و یک دور تازهٔ ممیزی کل repository در این مرحله اجرا نشدند. ادعای پنج چرخهٔ اولیه به snapshot قبلی محدود است.
