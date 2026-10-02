using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SahandNama;

public sealed record DiagnosticFinding(string Status, string Area, string Detail, string Remedy = "");

public sealed class DiagnosticReport
{
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public List<DiagnosticFinding> Findings { get; set; } = [];
    public string Summary => $"{Findings.Count(f => f.Status == "خطا")} خطا • {Findings.Count(f => f.Status == "هشدار")} هشدار • {Findings.Count(f => f.Status == "موفق")} بررسی موفق";

    public string ToText()
    {
        var text = new StringBuilder($"عیب‌یابی {Brand.Name} — {Created:yyyy-MM-dd HH:mm:ss}\n{Summary}\n\n");
        foreach (var finding in Findings)
        {
            text.AppendLine($"[{finding.Status}] {finding.Area}\n{finding.Detail}");
            if (finding.Remedy.Length > 0) text.AppendLine("راهکار: " + finding.Remedy);
            text.AppendLine();
        }
        return text.ToString();
    }
}

public sealed class AutoRepairResult
{
    public List<string> RepairedItems { get; set; } = [];
    public DiagnosticReport UpdatedReport { get; set; } = new();
    public string Summary => RepairedItems.Count == 0
        ? "هیچ ایراد خودکار قابل تعمیری یافت نشد (یا وضعیت از قبل سالم است)."
        : $"✓ {RepairedItems.Count} مورد خودکار تعمیر شد:\n• " + string.Join("\n• ", RepairedItems);
}

public static class Diagnostics
{
    public static async Task<DiagnosticReport> RunAsync(bool network, string? root = null)
    {
        root ??= Store.Root;
        var report = new DiagnosticReport();
        void Add(string status, string area, string detail, string remedy = "") => report.Findings.Add(new(status, area, detail, remedy));

        using (var version = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            Add("اطلاعات", "ویندوز و نشست", $"Edition: {version?.GetValue("EditionID")} | Build: {version?.GetValue("CurrentBuildNumber")} | Session: {Process.GetCurrentProcess().SessionId} | Process: {(Environment.Is64BitProcess ? "x64" : "x86")}");

        if (Process.GetCurrentProcess().SessionId == 0) Add("هشدار", "نشست کاربر", "برنامه در Session 0 اجرا می‌شود.", "اعمال دسکتاپ باید با task در نشست کاربر انجام شود، نه سرویس.");

        Preferences? settings = null;
        try
        {
            settings = Store.Read(Path.Combine(root, "settings.json"), new Preferences());
            var issues = ValidateSettings(settings);
            Add(issues.Count == 0 ? "موفق" : "خطا", "تنظیمات ذخیره‌شده", issues.Count == 0 ? "تنظیمات قابل خواندن و معتبر است." : string.Join("\n", issues), "از دکمه «تعمیر تنظیمات» یا «رفع خودکار تمام ایرادات» برای اصلاح مقادیر استفاده کنید.");
        }
        catch (Exception ex) { Add("خطا", "تنظیمات ذخیره‌شده", ex.Message, "فایل settings.json آسیب دیده است؛ با دکمه «رفع خودکار» بازنشانی می‌شود."); }

        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, "diagnostic-" + Guid.NewGuid().ToString("N") + ".tmp");
            try { await File.WriteAllTextAsync(probe, "write test"); }
            finally { if (File.Exists(probe)) File.Delete(probe); }
            var disk = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))!);
            Add(disk.AvailableFreeSpace < 500_000_000 ? "هشدار" : "موفق", "فضای ذخیره‌سازی و دسترسی نوشتن", $"{root}\nFree: {disk.AvailableFreeSpace / 1024 / 1024:N0} MB", "در صورت کمبود فضا، با دکمه «پاک‌سازی آرشیو» تصاویر قدیمی را پاک کنید.");
        }
        catch (Exception ex) { Add("خطا", "ذخیره‌سازی", ex.Message, "فضای دیسک، دسترسی حساب و Controlled Folder Access را بررسی کنید."); }

        await Task.Run(() =>
        {
            try
            {
                var archivePath = Path.Combine(root, "archive.json");
                var archive = Store.Read(archivePath, new List<Photo>());
                var missing = archive.Where(p => !File.Exists(p.FilePath)).ToList();
                var bad = new List<string>();
                var sample = archive.Where(p => File.Exists(p.FilePath)).Take(100).ToList();
                foreach (var photo in sample)
                    try { Store.LoadImage(photo.FilePath, 32); } catch { bad.Add(photo.FilePath); }

                Add(missing.Count + bad.Count > 0 ? "هشدار" : "موفق", "سلامت آرشیو و تصاویر", $"کل: {archive.Count} | مفقود: {missing.Count} | خراب در نمونه: {bad.Count} | بررسی decode: {sample.Count} (حداکثر ۱۰۰)\n" + string.Join("\n", missing.Select(p => p.FilePath).Concat(bad).Take(8)), "با دکمه «تعمیر آرشیو»، فایل‌های مفقود و خراب از پایگاه داده پاک‌سازی می‌شوند.");

                if (settings != null)
                {
                    foreach (var (label, enabled, isLock) in new[] { ("دسکتاپ", settings.Desktop, false), ("لاک‌اسکرین", settings.LockScreen, true) })
                    {
                        if (!enabled) continue;
                        var request = PhotoSelection.Request(settings, isLock);
                        if (request.Id == "Folder")
                            Add(Directory.Exists(request.Folder) ? "موفق" : "خطا", "پوشه " + label, request.Folder.Length == 0 ? "پوشه انتخاب نشده است." : request.Folder, "پوشه محلی قابل دسترسی با تصاویر JPG / PNG / BMP انتخاب کنید.");
                        if (request.Id == "Favorites")
                        {
                            var count = archive.Count(p => settings.Favorites.Contains(p.Id) && File.Exists(p.FilePath));
                            Add(count > 0 ? "موفق" : "خطا", "علاقه‌مندی‌های " + label, $"{count} تصویر موجود", "در گالری تصویرها را ستاره‌دار کنید.");
                        }
                    }
                }
            }
            catch (Exception ex) { Add("خطا", "آرشیو", ex.Message, "با دکمه «تعمیر آرشیو» فایل آرشیو را اصلاح کنید."); }
        });

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var key in new[] { @"SOFTWARE\Policies\Microsoft\Windows\Personalization", @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop", @"SOFTWARE\Microsoft\PolicyManager\current\device\Personalization", @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP" })
        {
            try
            {
                using var policy = hive.OpenSubKey(key);
                if (policy == null || policy.ValueCount == 0) continue;
                Add("هشدار", "سیاست‌های شخصی‌سازی", hive.Name + "\\" + key + "\n" + string.Join("\n", policy.GetValueNames().Select(n => $"{n} = {policy.GetValue(n)}")), "وجود مقدار لزوماً به معنی مسدودبودن نیست. مقادیر را با مدیر GPO/MDM بررسی کنید یا با دکمه «بازیابی سیاست» ریست کنید.");
            }
            catch (Exception ex) { Add("هشدار", "خواندن سیاست", ex.Message); }
        }

        var package = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy");
        var assets = Path.Combine(package, "LocalState", "Assets");
        try
        {
            var files = Directory.Exists(assets) ? new DirectoryInfo(assets).EnumerateFiles().ToList() : [];
            var newest = files.Count == 0 ? (DateTime?)null : files.Max(f => f.LastWriteTime);
            Add(files.Count == 0 || newest < DateTime.Now.AddDays(-14) ? "هشدار" : "موفق", "کش Spotlight ویندوز", $"Assets: {files.Count} | Latest: {newest?.ToString("yyyy-MM-dd HH:mm") ?? "ندارد"}", "برای بازنشانی کامل، از دکمه «بازنشانی کامل Spotlight» استفاده کنید.");
        }
        catch (Exception ex) { Add("هشدار", "کش Spotlight", ex.Message); }

        try
        {
            var json = await WindowsIntegration.RunPowerShellAsync($$"""
                Add-Type -AssemblyName System.Runtime.WindowsRuntime
                $api=$false; $fallback=$false; $apiError=''
                try { $null=[Windows.System.UserProfile.UserProfilePersonalizationSettings,Windows.System.UserProfile,ContentType=WindowsRuntime]; $api=[Windows.System.UserProfile.UserProfilePersonalizationSettings]::IsSupported(); $null=[Windows.System.UserProfile.LockScreen,Windows.System.UserProfile,ContentType=WindowsRuntime]; $fallback=$true } catch { $apiError=$_.Exception.Message }
                $task=Get-ScheduledTask -TaskName {{WindowsIntegration.QuotePS(WindowsIntegration.TaskName)}} -ErrorAction SilentlyContinue
                $info=$null; $action=$null
                if ($task) { $info=$task | Get-ScheduledTaskInfo; $action=$task.Actions | Select-Object -First 1 }
                $service=Get-CimInstance Win32_Service -Filter "Name='BingWallpaperProFeed'" -ErrorAction SilentlyContinue
                [ordered]@{ UserApi=$api; LockApi=$fallback; ApiError=$apiError; TaskInstalled=($null -ne $task); TaskState=[string]$task.State; TaskResult=$info.LastTaskResult; TaskLast=[string]$info.LastRunTime; TaskNext=[string]$info.NextRunTime; TaskExe=$action.Execute; TaskArguments=$action.Arguments; TaskPathExists=($action -and (Test-Path -LiteralPath $action.Execute)); TaskLogon=[string]$task.Principal.LogonType; ServiceInstalled=($null -ne $service); ServiceState=$service.State; ServiceStart=$service.StartMode; ServiceAccount=$service.StartName } | ConvertTo-Json -Compress
                """, 35);
            using var doc = JsonDocument.Parse(json); var fact = doc.RootElement;
            Add(fact.GetProperty("LockApi").GetBoolean() ? "موفق" : "هشدار", "APIهای لاک‌اسکرین", $"UserProfile API supported: {fact.GetProperty("UserApi")} | LockScreen API available: {fact.GetProperty("LockApi")}\n{fact.GetProperty("ApiError")}", "این بررسی فقط دسترسی API را می‌سنجد؛ نمایش واقعی با Win+L تأیید می‌شود.");
            if (!fact.GetProperty("TaskInstalled").GetBoolean()) Add("هشدار", "زمان‌بندی روزانه", "Task روزانه این کاربر نصب نیست.", "از دکمه «تعمیر زمان‌بندی» یا «فعال‌سازی زمان‌بندی» استفاده کنید.");
            else
            {
                var result = fact.GetProperty("TaskResult").ToString();
                var valid = fact.GetProperty("TaskPathExists").GetBoolean() &&
                            fact.GetProperty("TaskLogon").GetString() == "Interactive" &&
                            fact.GetProperty("TaskState").GetString() != "Disabled" &&
                            (fact.GetProperty("TaskArguments").GetString() ?? "").Contains("--update");
                Add(valid && result is "0" or "267009" or "267011" ? "موفق" : "هشدار", "زمان‌بندی روزانه", $"State: {fact.GetProperty("TaskState")} | Result: {result} | Last: {fact.GetProperty("TaskLast")} | Next: {fact.GetProperty("TaskNext")}\nExecute: {fact.GetProperty("TaskExe")} | Exists: {fact.GetProperty("TaskPathExists")} | Logon: {fact.GetProperty("TaskLogon")}", "در صورت بروز خطا، دکمه «تعمیر زمان‌بندی» برنامه را با مسیر فعلی دوباره ثبت می‌کند.");
            }
            if (!fact.GetProperty("ServiceInstalled").GetBoolean()) Add("اطلاعات", "سرویس دانلود", "سرویس نصب نیست؛ برای Task روزانه کاربر الزامی نیست.");
            else Add(fact.GetProperty("ServiceState").GetString() == "Running" ? "موفق" : "هشدار", "سرویس دانلود", $"State: {fact.GetProperty("ServiceState")} | Startup: {fact.GetProperty("ServiceStart")} | Account: {fact.GetProperty("ServiceAccount")}", "در صورت توقف، سرویس را مجدداً راه‌اندازی کنید.");
        }
        catch (Exception ex) { Add("هشدار", "API / Task Scheduler / سرویس", ex.Message, "بررسی میزبان کامل نشد؛ دسترسی PowerShell و Task Scheduler را بررسی کنید."); }

        if (network)
        {
            var market = settings != null && BingClient.Markets.Contains(settings.Market) ? settings.Market : "en-US";
            var probes = new[] { "Bing", "NasaDaily", "NasaLibrary", "WikimediaPotd", "EsaHubble" };
            report.Findings.AddRange(await Task.WhenAll(probes.Select(id => ProbeSourceAsync(id, market))));
        }
        else Add("اطلاعات", "شبکه", "آزمون آنلاین منابع خاموش است.");

        foreach (var file in new[] { "last-run.json", "activity.log" })
        {
            var path = Path.Combine(root, file);
            if (!File.Exists(path)) continue;
            try
            {
                var text = await File.ReadAllTextAsync(path);
                Add("اطلاعات", file, text.Length > 5000 ? text[^5000..] : text);
            }
            catch (Exception ex) { Add("هشدار", file, ex.Message); }
        }
        return report;
    }

    public static List<string> ValidateSettings(Preferences settings)
    {
        var problems = new List<string>();
        if (!settings.Desktop && !settings.LockScreen) problems.Add("حداقل یکی از مقصدهای دسکتاپ یا لاک‌اسکرین باید فعال باشد.");
        if (!TimeOnly.TryParseExact(settings.DailyTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) problems.Add("ساعت نامعتبر است (باید با قالب HH:mm باشد).");
        foreach (var source in new[] { settings.DesktopSource, settings.LockSource })
            if (!SourceCatalog.Options.Any(s => s.Id == source)) problems.Add("منبع ناشناخته: " + source);
        if (!BingClient.Markets.Contains(settings.Market) || !BingClient.Markets.Contains(settings.LockMarket)) problems.Add("منطقه Bing نامعتبر است.");
        if (!BingClient.Resolutions.Contains(settings.Resolution)) problems.Add("کیفیت تصویر نامعتبر است.");
        if (settings.DesktopMode is not ("Daily" or "Random")) problems.Add("روش انتخاب دسکتاپ نامعتبر است.");
        if (settings.LockMode is not ("Follow" or "Daily" or "Random" or "Previous")) problems.Add("روش انتخاب لاک‌اسکرین نامعتبر است.");
        if (settings.Mode is not ("Same" or "Previous" or "Regions" or "Random")) problems.Add("حالت انتخاب تصویر نامعتبر است.");
        return problems;
    }

    public static Preferences RepairSettings(string? root = null)
    {
        root ??= Store.Root;
        Directory.CreateDirectory(root);
        var settingsPath = Path.Combine(root, "settings.json");
        var repaired = new Preferences();
        try
        {
            if (File.Exists(settingsPath))
            {
                var current = Store.Read(settingsPath, new Preferences());
                repaired = current;
                if (!repaired.Desktop && !repaired.LockScreen) repaired.Desktop = true;
                if (!TimeOnly.TryParseExact(repaired.DailyTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) repaired.DailyTime = "09:00";
                if (!SourceCatalog.Options.Any(s => s.Id == repaired.DesktopSource)) repaired.DesktopSource = "Bing";
                if (!SourceCatalog.Options.Any(s => s.Id == repaired.LockSource)) repaired.LockSource = "Bing";
                if (!BingClient.Markets.Contains(repaired.Market)) repaired.Market = "en-US";
                if (!BingClient.Markets.Contains(repaired.LockMarket)) repaired.LockMarket = "en-GB";
                if (!BingClient.Resolutions.Contains(repaired.Resolution)) repaired.Resolution = "UHD";
                if (repaired.DesktopMode is not ("Daily" or "Random")) repaired.DesktopMode = repaired.Mode == "Random" ? "Random" : "Daily";
                if (repaired.LockMode is not ("Follow" or "Daily" or "Random" or "Previous"))
                {
                    repaired.LockMode = repaired.Mode switch
                    {
                        "Previous" => "Previous",
                        "Regions" => "Daily",
                        "Random" => "Random",
                        _ => "Follow"
                    };
                }
                if (repaired.Mode is not ("Same" or "Previous" or "Regions" or "Random")) repaired.Mode = "Same";
            }
        }
        catch
        {
            // If file was unparseable, backup old broken one and write fresh healthy defaults
            try
            {
                var backup = settingsPath + ".corrupt." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
                if (File.Exists(settingsPath)) File.Move(settingsPath, backup, true);
            }
            catch { }
            repaired = new Preferences();
        }
        Store.Write(settingsPath, repaired);
        Store.Log("Settings repaired and validated.", root);
        return repaired;
    }

    public static int RepairArchive(string? root = null)
    {
        root ??= Store.Root;
        var archivePath = Path.Combine(root, "archive.json");
        var list = new List<Photo>();
        var cleanedCount = 0;
        try
        {
            if (File.Exists(archivePath)) list = Store.Read(archivePath, new List<Photo>());
        }
        catch
        {
            list = [];
        }

        var valid = new List<Photo>();
        foreach (var p in list)
        {
            if (!File.Exists(p.FilePath)) { cleanedCount++; continue; }
            try
            {
                Store.LoadImage(p.FilePath, 32);
                valid.Add(p);
            }
            catch
            {
                cleanedCount++;
                try { File.Delete(p.FilePath); } catch { }
            }
        }

        // Also purge any orphaned 0-byte or corrupted tmp files in Images folder
        var imgDir = Path.Combine(root, "Images");
        if (Directory.Exists(imgDir))
        {
            foreach (var f in Directory.EnumerateFiles(imgDir, "*.tmp"))
            {
                try { File.Delete(f); cleanedCount++; } catch { }
            }
        }

        Store.Write(archivePath, valid.DistinctBy(p => p.Id).OrderByDescending(p => p.Date).ToList());
        var duplicatePurged = SourceCatalog.PurgeDuplicates(root);
        cleanedCount += duplicatePurged;
        Store.Log($"Archive repaired: {cleanedCount} corrupt/missing/duplicate items processed.", root);
        return cleanedCount;
    }

    public static async Task<AutoRepairResult> AutoRepairAllAsync(string? root = null)
    {
        root ??= Store.Root;
        var result = new AutoRepairResult();

        // 1. Repair Settings
        try
        {
            var settings = RepairSettings(root);
            result.RepairedItems.Add("تنظیمات برنامه بررسی، تصحیح و ذخیره شد.");
        }
        catch (Exception ex) { result.RepairedItems.Add("خطا در تعمیر تنظیمات: " + ex.Message); }

        // 2. Repair Archive, Cache, and Purge Duplicates
        try
        {
            var count = RepairArchive(root);
            result.RepairedItems.Add($"آرشیو و فایل‌های محلی پاک‌سازی و یکپارچه شدند ({count} فایل خراب، مفقود یا تکراری پاک شد).");
        }
        catch (Exception ex) { result.RepairedItems.Add("خطا در تعمیر آرشیو: " + ex.Message); }

        // 3. Ensure essential directories exist
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Images"));
            Directory.CreateDirectory(Path.Combine(root, "Backups"));
            result.RepairedItems.Add("پوشه‌های کاری برنامه (Images / Backups) بازسازی شدند.");
        }
        catch { }

        // 4. Repair Scheduled Task
        try
        {
            var exe = Environment.ProcessPath;
            if (exe != null && exe.EndsWith("BingWallpaperPro.exe", StringComparison.OrdinalIgnoreCase))
            {
                var time = Store.Read(Path.Combine(root, "settings.json"), new Preferences()).DailyTime;
                await WindowsIntegration.InstallScheduleAsync(time);
                result.RepairedItems.Add($"زمان‌بندی روزانه (ساعت {time}) و هنگام ورود مجدداً با مسیر برنامه ثبت و فعال شد.");
            }
        }
        catch (Exception ex) { result.RepairedItems.Add("ثبت زمان‌بندی: " + ex.Message); }

        // 5. Re-run diagnostics to get updated report
        result.UpdatedReport = await RunAsync(false, root);
        return result;
    }

    public static async Task<DiagnosticFinding> ProbeSourceAsync(string source, string market = "en-US")
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var url = source switch
            {
                "Bing" or "BingGlobal" => $"https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt={Uri.EscapeDataString(market)}",
                "NasaDaily" => SourceCatalog.NasaFeed,
                "NasaLibrary" => SourceCatalog.NasaLibrary,
                "WikimediaPotd" => SourceCatalog.WikimediaFeed,
                "EsaHubble" => SourceCatalog.EsaHubbleFeed,
                "UnsplashNature" => SourceCatalog.PicsumFeed,
                "UsgsEarthArt" => SourceCatalog.UsgsFeed,
                _ => throw new ArgumentException("منبع آنلاین ناشناخته")
            };

            var bytes = await SourceHttp.ReadAsync(url, 20_000_000);
            var count = source switch
            {
                "Bing" or "BingGlobal" => JsonDocument.Parse(bytes).RootElement.GetProperty("images").GetArrayLength(),
                "NasaDaily" => SourceCatalog.ParseNasaFeed(bytes).Count,
                "NasaLibrary" => SourceCatalog.ParseNasaLibrary(bytes).Count,
                "WikimediaPotd" => SourceCatalog.ParseWikimediaFeed(bytes).Count,
                "EsaHubble" => SourceCatalog.ParseEsaFeed(bytes).Count,
                "UnsplashNature" => SourceCatalog.ParsePicsum(bytes).Count,
                "UsgsEarthArt" => SourceCatalog.ParseUsgsFeed(bytes).Count,
                _ => 0
            };

            if (count == 0) throw new InvalidDataException("خوراک تصویر قابل استفاده ندارد.");
            return new("موفق", "اتصال " + source, $"HTTPS و محتوای خوراک معتبر؛ {count} تصویر دریافت شد؛ {watch.ElapsedMilliseconds} ms", "منبع آنلاین کاملاً در دسترس است.");
        }
        catch (Exception ex) { return new("خطا", "اتصال " + source, ex.Message, "اینترنت، DNS، پراکسی/VPN و دسترسی HTTPS به دامنه منبع را بررسی کنید."); }
    }
}
