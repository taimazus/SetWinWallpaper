using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace SahandNama;

public sealed class Preferences
{
    public string DesktopSource { get; set; } = "Bing";
    public string LockSource { get; set; } = "Bing";
    public string DesktopFolder { get; set; } = "";
    public string LockFolder { get; set; } = "";
    public string NetworkSharePath { get; set; } = "";
    public string LockNetworkSharePath { get; set; } = "";
    public string Market { get; set; } = "en-US";
    public string LockMarket { get; set; } = "en-GB";
    public string DesktopMode { get; set; } = "Daily";
    public string LockMode { get; set; } = "Follow";
    public string Mode { get; set; } = "Same";
    public string Resolution { get; set; } = "UHD";
    public string Fit { get; set; } = "Fill";
    public bool Desktop { get; set; } = true;
    public bool LockScreen { get; set; }
    public string DailyTime { get; set; } = "09:00";
    public bool IsServerMode { get; set; }
    public string ServerShareName { get; set; } = "Wallpapers";
    public List<string> Favorites { get; set; } = [];
    public bool EnableGlobalHotkeys { get; set; } = true;
    public bool ShowDesktopWidget { get; set; }
    public double WidgetLeft { get; set; } = -1;
    public double WidgetTop { get; set; } = -1;
    public bool SyncWindowsAccentColor { get; set; }
    public bool AutoCleanDesktopIcons { get; set; }
    public double WidgetOpacity { get; set; } = 0.95;
    public string WidgetPinMode { get; set; } = "Desktop"; // "Desktop", "Normal", "TopMost"
    public bool WidgetShowClock { get; set; } = true;
    public bool WidgetShowWallpaperInfo { get; set; } = true;
    public bool WidgetShowQuickActions { get; set; } = true;
    public bool WidgetShowWeather { get; set; } = true;
    public string WidgetCity { get; set; } = "تهران";
    public bool WidgetShowHardware { get; set; } = true;
}

public sealed class Photo
{
    public string Source { get; set; } = "Bing";
    public string SourcePage { get; set; } = "";
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Copyright { get; set; } = "";
    public string Date { get; set; } = "";
    public string Url { get; set; } = "";
    public string Market { get; set; } = "";
    public string FilePath { get; set; } = "";
    [JsonIgnore] public string Caption => $"{Date}  ·  {Source}  ·  {Market}";
}

public static class Store
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BingWallpaperPro");
    public static string SharedRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BingWallpaperPro", "Feed");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static T Read<T>(string path, T fallback)
    {
        if (!File.Exists(path)) return fallback;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ?? throw new InvalidDataException($"Invalid JSON: {path}");
    }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value, Json)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static Preferences Settings => Read(Path.Combine(Root, "settings.json"), new Preferences());
    public static void Save(Preferences settings) => Write(Path.Combine(Root, "settings.json"), settings);
    public static Preferences ReadSettingsForStartup(string? root = null)
    {
        root ??= Root;
        try { return Read(Path.Combine(root, "settings.json"), new Preferences()); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Log("Settings could not be loaded; repair is required: " + ex.Message, root);
            return new Preferences();
        }
    }
    public static Preferences UpdateSettings(Action<Preferences> update, string? root = null)
    {
        root ??= Root;
        using var gate = AcquireLockAsync(Path.Combine(root, "settings.lock")).GetAwaiter().GetResult();
        var settings = Read(Path.Combine(root, "settings.json"), new Preferences());
        update(settings);
        Write(Path.Combine(root, "settings.json"), settings);
        return settings;
    }
    public static Preferences CloneSettings(Preferences settings) => JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(settings, Json), Json)!;
    public static Preferences SaveEditorSettings(Preferences edited, Preferences baseline, string? root = null) => UpdateSettings(current =>
    {
        foreach (var property in typeof(Preferences).GetProperties())
        {
            if (property.Name is nameof(Preferences.Favorites) or nameof(Preferences.WidgetLeft) or nameof(Preferences.WidgetTop)) continue;
            var value = property.GetValue(edited);
            if (!Equals(value, property.GetValue(baseline))) property.SetValue(current, value);
        }
    }, root);
    public static void MergeArchive(string root, IEnumerable<Photo> photos)
    {
        using var gate = AcquireLockAsync(Path.Combine(root, "archive.lock")).GetAwaiter().GetResult();
        var path = Path.Combine(root, "archive.json");
        Write(path, photos.Concat(Read(path, new List<Photo>())).DistinctBy(p => p.Id).OrderByDescending(p => p.Date).ToList());
    }
    public static void RecordDesktopPhoto(Photo photo, string? root = null) => Write(Path.Combine(root ?? Root, "current-desktop.json"), photo);
    public static Photo? CurrentDesktopPhoto(string? root = null)
    {
        try { return Read<Photo?>(Path.Combine(root ?? Root, "current-desktop.json"), null); }
        catch (Exception ex) { Log("Current wallpaper metadata could not be loaded: " + ex.Message, root); return null; }
    }
    public static bool IsOwnedFile(string path, string directory)
    {
        try
        {
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return false;
            if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) return false;
            for (var parent = new DirectoryInfo(directory); parent != null; parent = parent.Parent)
                if (parent.Exists && parent.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
            return true;
        }
        catch { return false; }
    }
    public static void Log(string message, string? root = null)
    {
        try
        {
            root ??= Root; Directory.CreateDirectory(root);
            var path = Path.Combine(root, "activity.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}");
        }
        catch { /* Logging must not crash a background process. */ }
    }
    public static async Task<FileStream> AcquireLockAsync(string path, int timeoutSeconds = 10, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var start = DateTime.UtcNow;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow - start < TimeSpan.FromSeconds(timeoutSeconds))
            { await Task.Delay(200, token).ConfigureAwait(false); }
        }
    }
    public static BitmapImage LoadImage(string path, int width = 1600)
    {
        using var stream = File.OpenRead(path);
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (width > 0) bitmap.DecodePixelWidth = width;
        bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }
}

public sealed class BingClient
{
    public bool UsedOfflineFallback { get; private set; }
    public static readonly string[] Markets = ["en-US", "en-GB", "de-DE", "fr-FR", "ja-JP", "en-CA", "en-AU", "en-IN", "zh-CN", "pt-BR", "it-IT", "es-ES"];
    public static readonly string[] Resolutions = ["UHD", "1920x1080", "1366x768"];
    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    readonly string root;
    readonly HttpClient http;
    public BingClient(string? root = null) : this(root, Http) { }
    internal BingClient(string? root, HttpClient http) { this.root = root ?? Store.Root; this.http = http; }
    public static Uri TrustedUri(string url)
    {
        var uri = new Uri(new Uri("https://www.bing.com"), url);
        if (uri.Scheme != "https" || uri.Host != "www.bing.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new InvalidDataException("Only HTTPS images hosted by www.bing.com are accepted.");
        return uri;
    }
    public async Task<List<Photo>> FetchAsync(string market, string resolution, CancellationToken token = default, bool allowOffline = true)
        => await FetchCoreAsync(market, resolution, token, allowOffline, false);
    internal Task<List<Photo>> FetchUnderFeedLockAsync(string market, string resolution, CancellationToken token, bool allowOffline)
        => FetchCoreAsync(market, resolution, token, allowOffline, true);
    async Task<List<Photo>> FetchCoreAsync(string market, string resolution, CancellationToken token, bool allowOffline, bool lockHeld)
    {
        if (!Markets.Contains(market) || !Resolutions.Contains(resolution)) throw new ArgumentException("Invalid region or resolution.");
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "archive.json");
        await using var gate = lockHeld ? null : await Store.AcquireLockAsync(Path.Combine(root, "feed.lock"), 10, token);
        var existingArchive = Store.Read(archivePath, new List<Photo>());

        try
        {
            using var document = JsonDocument.Parse(await SourceHttp.ReadAsync(TrustedUri($"/HPImageArchive.aspx?format=js&idx=0&n=8&mkt={market}").AbsoluteUri, 20_000_000, token, http));
            var result = new List<Photo>();

            foreach (var item in document.RootElement.GetProperty("images").EnumerateArray())
            {
                var url = TrustedUri(item.GetProperty("urlbase").GetString() + "_" + resolution + ".jpg").AbsoluteUri;
                var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24];
                var filePath = Path.Combine(root, "Images", id + ".jpg");

                // Check if photo is already downloaded and healthy in existing archive or disk
                var existing = existingArchive.FirstOrDefault(p => (p.Id == id || p.Url == url) && File.Exists(p.FilePath) && new FileInfo(p.FilePath).Length > 1000);
                if (existing != null)
                {
                    try { Store.LoadImage(existing.FilePath, 32); filePath = existing.FilePath; id = existing.Id; }
                    catch { existing = null; }
                }

                var photo = new Photo
                {
                    Id = id, Url = url, Market = market, Date = item.GetProperty("startdate").GetString()!,
                    Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "Bing" : "Bing",
                    Copyright = item.GetProperty("copyright").GetString() ?? "", FilePath = filePath
                };
                photo.SourcePage = item.TryGetProperty("copyrightlink", out var creditLink) ? creditLink.GetString() ?? "" : "";
                
                if (existing == null || !File.Exists(photo.FilePath))
                {
                    await DownloadAsync(photo, token);
                }
                result.Add(photo);
            }
            if (result.Count == 0) throw new InvalidDataException("Bing returned no images.");
            Store.MergeArchive(root, result);
            return result;
        }
        catch (Exception ex) when (allowOffline && SourceHttp.IsRecoverable(ex, token))
        {
            var validOffline = existingArchive.Where(p => p.Source is "Bing" or "BingGlobal" && p.Market == market && SourceCatalog.IsHealthy(p)).ToList();
            if (validOffline.Count > 0)
            {
                UsedOfflineFallback = true;
                Store.Log($"ارتباط با سرور بینگ برقرار نشد ({ex.Message}). استفاده خودکار از {validOffline.Count} تصویر آرشیو محلی.", root);
                return validOffline;
            }
            throw;
        }
    }
    async Task DownloadAsync(Photo photo, CancellationToken token)
    {
        if (File.Exists(photo.FilePath) && new FileInfo(photo.FilePath).Length > 1000)
        {
            try { Store.LoadImage(photo.FilePath, 32); return; }
            catch { try { File.Delete(photo.FilePath); } catch { } }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(photo.FilePath)!);
        for (var attempt = 0; ; attempt++)
        {
            var temp = photo.FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(120));
                var transferToken = deadline.Token;
                using var response = await http.GetAsync(TrustedUri(photo.Url), HttpCompletionOption.ResponseHeadersRead, transferToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 150_000_000) throw new InvalidDataException("Image exceeds 150 MB.");
                await using (var source = await response.Content.ReadAsStreamAsync(transferToken))
                await using (var destination = File.Create(temp))
                {
                    var buffer = new byte[81920]; long total = 0; int count;
                    while ((count = await source.ReadAsync(buffer, transferToken)) != 0)
                    {
                        total += count;
                        if (total > 150_000_000) throw new InvalidDataException("Image exceeds 150 MB.");
                        await destination.WriteAsync(buffer.AsMemory(0, count), transferToken);
                    }
                }
                Store.LoadImage(temp, 32); File.Move(temp, photo.FilePath, true); return;
            }
            catch (HttpRequestException) when (attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(attempt + 1), token); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}

public sealed class WallpaperEngine
{
    public async Task UpdateAsync(bool scheduled = false)
    {
        Directory.CreateDirectory(Store.Root);
        await using var gate = await Store.AcquireLockAsync(Path.Combine(Store.Root, "update.lock"), 10);
        var settings = Store.Settings;
        if (!settings.Desktop && !settings.LockScreen) throw new InvalidOperationException("حداقل یک مقصد را انتخاب کنید.");
        var catalog = new SourceCatalog();
        var day = DateOnly.FromDateTime(DateTime.Now);
        var selection = new PhotoSelectionSession(settings, request => catalog.FetchAsync(request), day);
        var messages = new List<string>();
        var failed = false;
        if (settings.IsServerMode)
        {
            try
            {
                var synced = await catalog.SyncAllOnlineSourcesAsync(settings.Resolution);
                messages.Add($"همگام‌سازی مخزن سرور: {synced} تصویر از تمامی گالری‌های آنلاین دریافت و به‌روزرسانی شد.");
            }
            catch (Exception ex) { failed = true; messages.Add("همگام‌سازی مخزن سرور: " + ex.Message); }
        }

        if (settings.Desktop)
        {
            try
            {
                var desktopPhoto = await selection.SelectAsync(false);
                WindowsIntegration.SetDesktop(desktopPhoto.FilePath, settings.Fit);
                Store.RecordDesktopPhoto(desktopPhoto);
                messages.Add("تصویر دسکتاپ تغییر کرد.");
                if (settings.SyncWindowsAccentColor && File.Exists(desktopPhoto.FilePath))
                {
                    try { WindowsIntegration.SyncWindowsAccentColor(desktopPhoto.FilePath); }
                    catch { }
                }
            }
            catch (Exception ex) { failed = true; messages.Add("تغییر دسکتاپ ناموفق بود: " + ex.Message); }
        }
        if (settings.LockScreen)
        {
            try { await WindowsIntegration.SetLockScreenAsync((await selection.SelectAsync(true)).FilePath); messages.Add("ویندوز درخواست تغییر لاک‌اسکرین را پذیرفت."); }
            catch (Exception ex) { failed = true; messages.Add("تغییر لاک‌اسکرین ناموفق بود: " + ex.Message); }
        }
        if (scheduled && catalog.UsedOfflineFallback)
        {
            failed = true;
            messages.Add("دریافت آنلاین کامل نشد؛ تصویر قابل استفاده اعمال شد. زمان‌بندی یک ساعت بعد دوباره تلاش می‌کند.");
        }
        var outcome = string.Join(Environment.NewLine, messages); Store.Log(outcome);
        Store.Write(Path.Combine(Store.Root, "last-run.json"), new { Time = DateTimeOffset.Now, Result = outcome });
        if (failed) throw new InvalidOperationException(outcome);
    }
}
