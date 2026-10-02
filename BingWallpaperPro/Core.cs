using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace BingWallpaperPro;

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
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow - start < TimeSpan.FromSeconds(timeoutSeconds))
            { await Task.Delay(200, token); }
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
    public static readonly string[] Markets = ["en-US", "en-GB", "de-DE", "fr-FR", "ja-JP", "en-CA", "en-AU", "en-IN", "zh-CN"];
    public static readonly string[] Resolutions = ["UHD", "1920x1080", "1366x768"];
    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    readonly string root;
    public BingClient(string? root = null) => this.root = root ?? Store.Root;
    public static Uri TrustedUri(string url)
    {
        var uri = new Uri(new Uri("https://www.bing.com"), url);
        if (uri.Scheme != "https" || uri.Host != "www.bing.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new InvalidDataException("Only HTTPS images hosted by www.bing.com are accepted.");
        return uri;
    }
    public async Task<List<Photo>> FetchAsync(string market, string resolution, CancellationToken token = default)
    {
        if (!Markets.Contains(market) || !Resolutions.Contains(resolution)) throw new ArgumentException("Invalid region or resolution.");
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "archive.json");
        var existingArchive = Store.Read(archivePath, new List<Photo>());

        try
        {
            await using var gate = await Store.AcquireLockAsync(Path.Combine(root, "feed.lock"), 10, token);
            using var response = await Http.GetAsync(TrustedUri($"/HPImageArchive.aspx?format=js&idx=0&n=8&mkt={market}"), token);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
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
            Store.Write(archivePath, result.Concat(existingArchive).DistinctBy(p => p.Id).OrderByDescending(p => p.Date).ToList());
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Net.Sockets.SocketException or TimeoutException)
        {
            var validOffline = existingArchive.Where(p => File.Exists(p.FilePath) && new FileInfo(p.FilePath).Length > 1000).ToList();
            if (validOffline.Count > 0)
            {
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
                using var response = await Http.GetAsync(TrustedUri(photo.Url), HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 150_000_000) throw new InvalidDataException("Image exceeds 150 MB.");
                await using (var source = await response.Content.ReadAsStreamAsync(token))
                await using (var destination = File.Create(temp))
                {
                    var buffer = new byte[81920]; long total = 0; int count;
                    while ((count = await source.ReadAsync(buffer, token)) != 0)
                    {
                        total += count;
                        if (total > 150_000_000) throw new InvalidDataException("Image exceeds 150 MB.");
                        await destination.WriteAsync(buffer.AsMemory(0, count), token);
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
    public async Task UpdateAsync()
    {
        Directory.CreateDirectory(Store.Root);
        await using var gate = await Store.AcquireLockAsync(Path.Combine(Store.Root, "update.lock"), 10);
        var settings = Store.Settings;
        if (!settings.Desktop && !settings.LockScreen) throw new InvalidOperationException("حداقل یک مقصد را انتخاب کنید.");
        var catalog = new SourceCatalog();
        var fetched = new Dictionary<SourceRequest, Task<List<Photo>>>();
        var day = DateOnly.FromDateTime(DateTime.Now);
        async Task<Photo> SelectAsync(bool lockScreen)
        {
            var request = PhotoSelection.Request(settings, lockScreen);
            if (!fetched.TryGetValue(request, out var fetch)) fetched[request] = fetch = catalog.FetchAsync(request);
            var photos = await fetch;
            return PhotoSelection.Select(photos, request.Id, lockScreen && settings.Mode == "Previous", day);
        }
        var messages = new List<string>();
        var failed = false;
        if (settings.IsServerMode)
        {
            try
            {
                var synced = await catalog.SyncAllOnlineSourcesAsync(settings.Resolution);
                messages.Add($"همگام‌سازی مخزن سرور: {synced} تصویر از تمامی گالری‌های آنلاین دریافت و به‌روزرسانی شد.");
            }
            catch (Exception ex) { messages.Add("همگام‌سازی مخزن سرور: " + ex.Message); }
        }

        if (settings.Desktop)
        {
            try
            {
                var desktopPhoto = await SelectAsync(false);
                WindowsIntegration.SetDesktop(desktopPhoto.FilePath, settings.Fit);
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
            try { await WindowsIntegration.SetLockScreenAsync((await SelectAsync(true)).FilePath); messages.Add("ویندوز درخواست تغییر لاک‌اسکرین را پذیرفت."); }
            catch (Exception ex) { failed = true; messages.Add("تغییر لاک‌اسکرین ناموفق بود: " + ex.Message); }
        }
        var outcome = string.Join(Environment.NewLine, messages); Store.Log(outcome);
        Store.Write(Path.Combine(Store.Root, "last-run.json"), new { Time = DateTimeOffset.Now, Result = outcome });
        if (failed) throw new InvalidOperationException(outcome);
    }
}
