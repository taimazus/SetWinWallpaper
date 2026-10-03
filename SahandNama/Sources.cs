using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SahandNama;

public sealed record SourceOption(string Id, string Name);
public sealed record SourceRequest(string Id, string Market, string Resolution, string Folder, string NetworkShare = "");

public static class PhotoSelection
{
    public static SourceRequest Request(Preferences settings, bool lockScreen)
    {
        if (settings.Mode is not ("Same" or "Previous" or "Regions" or "Random")) throw new InvalidDataException("حالت انتخاب تصویر ناشناخته است.");
        var independent = lockScreen && LockMode(settings) is "Daily" or "Random";
        return new SourceRequest(
            independent ? settings.LockSource : settings.DesktopSource,
            independent ? settings.LockMarket : settings.Market,
            settings.Resolution,
            independent ? settings.LockFolder : settings.DesktopFolder,
            independent ? settings.LockNetworkSharePath : settings.NetworkSharePath);
    }
    public static string DesktopMode(Preferences settings) => settings.DesktopMode == "Random" || (settings.LockMode == "Follow" && settings.Mode == "Random") ? "Random" : "Daily";
    public static string LockMode(Preferences settings) => settings.LockMode == "Follow" ? settings.Mode switch { "Regions" => "Daily", "Previous" => "Previous", _ => "Follow" } : settings.LockMode;
    public static Photo Select(IReadOnlyList<Photo> photos, string source, bool previous, DateOnly day, bool random = false)
    {
        if (photos.Count == 0) throw new InvalidOperationException("منبع انتخاب‌شده تصویر قابل استفاده ندارد.");
        if (previous && photos.Count < 2) throw new InvalidOperationException("برای تصویر قبلی، حداقل دو تصویر در منبع لازم است.");
        if (random && photos.Count > 1)
        {
            return photos[Random.Shared.Next(photos.Count)];
        }
        // Daily feeds use newest-first. Collections rotate deterministically each local calendar day.
        var isDailyFeed = source is "Bing" or "NasaDaily" or "WikimediaPotd" or "EsaHubble" or "Wallhaven" or "MuseumArt" or "NatGeoNature" or "CyberpunkArt" or "Architecture4K";
        var index = isDailyFeed ? 0 : Math.Abs(day.DayNumber) % photos.Count;
        return photos[(index + (previous ? 1 : 0)) % photos.Count];
    }
}

public sealed class PhotoSelectionSession(Preferences settings, Func<SourceRequest, Task<List<Photo>>> fetch, DateOnly day)
{
    readonly Dictionary<SourceRequest, Task<List<Photo>>> fetched = new();
    Task<Photo>? desktop;
    Task<List<Photo>> Photos(SourceRequest request)
    {
        if (!fetched.TryGetValue(request, out var task)) fetched[request] = task = fetch(request);
        return task;
    }
    async Task<Photo> DesktopAsync()
    {
        var request = PhotoSelection.Request(settings, false);
        return PhotoSelection.Select(await Photos(request), request.Id, false, day, PhotoSelection.DesktopMode(settings) == "Random");
    }
    public async Task<Photo> SelectAsync(bool lockScreen)
    {
        if (!lockScreen || PhotoSelection.LockMode(settings) == "Follow") return await (desktop ??= DesktopAsync());
        if (PhotoSelection.LockMode(settings) == "Previous")
        {
            var selected = await (desktop ??= DesktopAsync());
            var photos = await Photos(PhotoSelection.Request(settings, false));
            if (photos.Count < 2) throw new InvalidOperationException("برای تصویر قبلی، حداقل دو تصویر در منبع لازم است.");
            var index = photos.FindIndex(p => p.Id == selected.Id);
            return photos[(index + 1) % photos.Count];
        }
        var request = PhotoSelection.Request(settings, true);
        return PhotoSelection.Select(await Photos(request), request.Id, false, day, PhotoSelection.LockMode(settings) == "Random");
    }
}

public static partial class SourceHttp
{
    static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    static SourceHttp() => Client.DefaultRequestHeaders.UserAgent.ParseAdd("SahandNama/1.8.1 (Windows desktop wallpaper manager; +https://irres.ir)");

    public static Uri Validate(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            throw new InvalidDataException("نشانی منبع در فهرست میزبان‌های مجاز HTTPS نیست.");

        var host = uri.Host.ToLowerInvariant();
        var isAllowed = host is "www.bing.com" or "www.nasa.gov" or "images-api.nasa.gov" or "images-assets.nasa.gov" or "images.nasa.gov"
            or "commons.wikimedia.org" or "upload.wikimedia.org" or "thumb.wikimedia.org"
            or "esahubble.org" or "cdn.esahubble.org" or "www.eso.org" or "cdn.eso.org"
            or "picsum.photos" or "fastly.picsum.photos" or "images.unsplash.com"
            or "eros.usgs.gov" or "landsat.usgs.gov" or "pubs.usgs.gov" or "earthexplorer.usgs.gov"
            or "wallhaven.cc" or "w.wallhaven.cc" or "th.wallhaven.cc"
            or "api.artic.edu" or "www.artic.edu" or "artic.edu";

        if (!isAllowed)
            throw new InvalidDataException("میزبان منبع مجاز نیست: " + uri.Host);

        return uri;
    }

    public static bool IsRecoverable(Exception ex, CancellationToken caller) => ex is HttpRequestException or IOException or InvalidDataException or System.Net.Sockets.SocketException or TimeoutException or XmlException or JsonException || ex is OperationCanceledException && !caller.IsCancellationRequested;
    static async Task<HttpResponseMessage> ResponseAsync(HttpClient client, string url, CancellationToken token)
    {
        var uri = Validate(url);
        for (var hop = 0; ; hop++)
        {
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (hop >= 5 || location == null) throw new InvalidDataException("Invalid or excessive source redirects.");
                uri = Validate(new Uri(uri, location).AbsoluteUri);
                continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; }
            catch { response.Dispose(); throw; }
        }
    }
    public static async Task<byte[]> ReadAsync(string url, int limit = 20_000_000, CancellationToken cancellation = default) => await ReadAsync(url, limit, cancellation, Client);
    internal static async Task<byte[]> ReadAsync(string url, int limit, CancellationToken cancellation, HttpClient client)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        using var response = await ResponseAsync(client, url, token);
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("حجم پاسخ منبع بیش از حد مجاز است.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream(); var buffer = new byte[81920]; int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("حجم پاسخ منبع بیش از حد مجاز است.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    public static async Task DownloadToFileAsync(string url, string targetPath, long limit = 150_000_000, CancellationToken cancellation = default) => await DownloadToFileAsync(url, targetPath, limit, cancellation, Client);
    internal static async Task DownloadToFileAsync(string url, string targetPath, long limit, CancellationToken cancellation, HttpClient client)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        var token = deadline.Token;
        using var response = await ResponseAsync(client, url, token);
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("حجم فایل تصویر بیش از حد مجاز است.");

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        await using var fs = File.Create(targetPath);
        var buffer = new byte[81920]; long total = 0; int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException("حجم فایل تصویر بیش از حد مجاز است.");
            await fs.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }
}

public sealed class SourceCatalog
{
    public bool UsedOfflineFallback { get; private set; }
    public static readonly SourceOption[] Options =
    [
        new("Bing", "Microsoft Bing — تصویر روز"),
        new("BingGlobal", "گلچین بین‌المللی بینگ (تمام قاره‌ها و کشورها)"),
        new("Wallhaven", "Wallhaven — والپیپرهای برگزیده (خروجی تا ضلع 3840)"),
        new("IranNature", "ایران زیبا — طبیعت، کوهستان‌ها و میراث باستانی ایران"),
        new("MuseumArt", "موزه هنر شیکاگو — نقاشی‌های آزاد در Wikimedia"),
        new("NatGeoNature", "Wikimedia — مناظر برگزیدهٔ بین‌المللی"),
        new("UnsplashNature", "Unsplash & Picsum — عکاسی طبیعت و مناظر 4K"),
        new("WikimediaPotd", "Wikimedia Commons — تصویر منتخب روز"),
        new("Spotlight", "Microsoft Spotlight — کش محلی"),
        new("CyberpunkArt", "هنر دیجیتال، سایبرپانک و فانتزی 4K"),
        new("Architecture4K", "معماری مدرن و چشم‌اندازهای شهری 4K"),
        new("UsgsEarthArt", "USGS Earth as Art — شگفتی‌های زمین از فضا"),
        new("NasaDaily", "NASA — تصویر روز (Image of the Day)"),
        new("NasaLibrary", "NASA — کتابخانه تصاویر فضا"),
        new("EsaHubble", "ESA / Hubble & Webb — رصدخانه‌های فضایی"),
        new("SharedNetwork", "مخزن اشتراکی سرور (شبکه سازمانی / UNC)"),
        new("Folder", "پوشه دلخواه — گردش روزانه"),
        new("Favorites", "علاقه‌مندی‌ها — گردش روزانه")
    ];

    public const string NasaFeed = "https://www.nasa.gov/feeds/iotd-feed/";
    public const string NasaLibrary = "https://images-api.nasa.gov/search?q=nebula&media_type=image&page_size=12";
    public const string WikimediaFeed = "https://commons.wikimedia.org/w/api.php?action=featuredfeed&feed=potd&feedformat=rss";
    public const string EsaHubbleFeed = "https://esahubble.org/images/potw/feed/";
    public const string PicsumFeed = "https://picsum.photos/v2/list?page=1&limit=30";
    public const string UsgsFeed = "https://eros.usgs.gov/earth-as-art/feed";
    public const string WallhavenTopFeed = "https://wallhaven.cc/api/v1/search?sorting=toplist&topRange=1M&purity=100&resolutions=3840x2160,2560x1440,1920x1080";
    public const string WallhavenCyberFeed = "https://wallhaven.cc/api/v1/search?q=cyberpunk&sorting=toplist&topRange=1M&purity=100&resolutions=3840x2160,2560x1440,1920x1080";
    public const string WallhavenArchFeed = "https://wallhaven.cc/api/v1/search?q=architecture&sorting=toplist&topRange=1M&purity=100&resolutions=3840x2160,2560x1440,1920x1080";
    public static string MuseumArtFeed => CommonsImageFeed("incategory:\"Paintings in the Art Institute of Chicago\"", 25);
    internal static string CommonsImageFeed(string query, int limit = 3) => "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrsearch=" + Uri.EscapeDataString(query + " filetype:bitmap") + "&gsrnamespace=6&gsrlimit=" + limit + "&prop=imageinfo&iiprop=url%7Csize%7Cmime%7Cextmetadata&iiurlwidth=1920";
    public static string FeaturedNatureFeed => CommonsImageFeed("incategory:\"Featured pictures of landscapes\"", 25);

    readonly string root;
    readonly HttpClient? http;
    public SourceCatalog(string? root = null) => this.root = root ?? Store.Root;
    internal SourceCatalog(string root, HttpClient http) { this.root = root; this.http = http; }
    internal static bool IsHealthy(Photo photo)
    {
        try { if (!File.Exists(photo.FilePath)) return false; Store.LoadImage(photo.FilePath, 32); return true; }
        catch { return false; }
    }

    public async Task<List<Photo>> FetchAsync(SourceRequest request, CancellationToken token = default, bool allowOffline = true)
    {
        if (!Options.Any(o => o.Id == request.Id)) throw new ArgumentException("منبع ناشناخته است: " + request.Id);
        if (request.Id == "Bing")
        {
            var bing = http == null ? new BingClient(root) : new BingClient(root, http);
            var result = await bing.FetchAsync(request.Market, request.Resolution, token, allowOffline);
            UsedOfflineFallback |= bing.UsedOfflineFallback;
            return result;
        }
        if (request.Id == "BingGlobal") return await FetchBingGlobalAsync(request.Resolution, token, allowOffline);
        if (request.Id == "IranNature") return await FetchIranNatureAsync(token, allowOffline);
        if (request.Id == "Favorites")
        {
            var settings = Store.Read(Path.Combine(root, "settings.json"), new Preferences());
            var favorites = Store.Read(Path.Combine(root, "archive.json"), new List<Photo>())
                .Where(p => settings.Favorites.Contains(p.Id) && File.Exists(p.FilePath)).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
            if (favorites.Count == 0) throw new InvalidOperationException("تصویری در علاقه‌مندی‌ها نیست؛ ابتدا در گالری تصویرها را ستاره‌دار کنید.");
            return favorites;
        }
        if (request.Id == "Spotlight") return await Task.Run(() => WindowsIntegration.ImportSpotlight(root), token);
        if (request.Id == "SharedNetwork") return await FetchSharedNetworkAsync(request.NetworkShare, token);

        Directory.CreateDirectory(root);
        await using var gate = await Store.AcquireLockAsync(Path.Combine(root, "feed.lock"), 10, token);
        List<Photo> photos;
        string? sourceFailure = null;
        if (request.Id == "Folder") photos = await Task.Run(() => ImportFolder(request.Folder, token), token);
        else
        {
            var url = request.Id switch
            {
                "NasaDaily" => NasaFeed,
                "NasaLibrary" => NasaLibrary,
                "WikimediaPotd" => WikimediaFeed,
                "EsaHubble" => EsaHubbleFeed,
                "UnsplashNature" => PicsumFeed,
                "UsgsEarthArt" => UsgsFeed,
                "Wallhaven" => WallhavenTopFeed,
                "CyberpunkArt" => WallhavenCyberFeed,
                "Architecture4K" => WallhavenArchFeed,
                "MuseumArt" => MuseumArtFeed,
                "NatGeoNature" => FeaturedNatureFeed,
                _ => throw new ArgumentException("منبع آنلاین ناشناخته: " + request.Id)
            };

        try
        {
            var bytes = http == null ? await SourceHttp.ReadAsync(url, 20_000_000, token) : await SourceHttp.ReadAsync(url, 20_000_000, token, http);
            var candidates = request.Id switch
            {
                "NasaDaily" => ParseNasaFeed(bytes),
                "NasaLibrary" => ParseNasaLibrary(bytes),
                "WikimediaPotd" => ParseWikimediaFeed(bytes),
                "EsaHubble" => ParseEsaFeed(bytes),
                "UnsplashNature" => ParsePicsum(bytes),
                "UsgsEarthArt" => ParseUsgsFeed(bytes),
                "Wallhaven" or "CyberpunkArt" or "Architecture4K" => ParseWallhaven(bytes, request.Id),
                "MuseumArt" => ParseCommonsImages(bytes, "MuseumArt"),
                "NatGeoNature" => ParseCommonsImages(bytes, "NatGeoNature"),
                _ => []
            };

            photos = [];
            var takeCount = request.Id is "NasaDaily" or "WikimediaPotd" or "EsaHubble" ? 6 : 15;
            foreach (var photo in candidates.Take(takeCount))
            {
                token.ThrowIfCancellationRequested();
                try { await DownloadAsync(photo, token); photos.Add(photo); }
                catch (Exception ex) when (ex is HttpRequestException or IOException or NotSupportedException or ArgumentException || ex is OperationCanceledException && !token.IsCancellationRequested)
                {
                    sourceFailure = ex.Message;
                    if (ex is HttpRequestException or OperationCanceledException) UsedOfflineFallback = true;
                    Store.Log($"Skipped {request.Id} image {photo.Url}: {ex.Message}", root);
                }
            }
        }
        catch (Exception ex) when (allowOffline && SourceHttp.IsRecoverable(ex, token))
        {
            sourceFailure = ex.Message;
            Store.Log($"ارتباط با منبع {request.Id} برقرار نشد ({ex.Message}). تلاش برای استفاده از آرشیو محلی...", root);
            photos = [];
        }
        }
        if (photos.Count == 0)
        {
            var fallback = Store.Read(Path.Combine(root, "archive.json"), new List<Photo>())
                .Where(p => allowOffline && request.Id != "Folder" && p.Source == request.Id && IsHealthy(p))
                .ToList();
            if (fallback.Count > 0)
            {
                UsedOfflineFallback = true;
                Store.Log($"استفاده خودکار از {fallback.Count} تصویر موجود در آرشیو محلی برای منبع {request.Id}.", root);
                return fallback;
            }
            throw new InvalidOperationException($"دریافت تصویر از منبع {request.Id} ناموفق بود و تصویر سالمی در آرشیو نیست. علت: {sourceFailure ?? "منبع تصویر قابل دانلود برنگرداند."}");
        }
        MergeArchive(photos); return photos;
    }

    public async Task<List<Photo>> FetchBingGlobalAsync(string resolution, CancellationToken token = default, bool allowOffline = true)
    {
        await using var gate = await Store.AcquireLockAsync(Path.Combine(root, "feed.lock"), 10, token);
        var markets = new[] { "en-US", "ja-JP", "de-DE", "fr-FR", "en-GB", "zh-CN", "pt-BR", "it-IT", "es-ES", "en-CA", "en-AU", "en-IN" };
        var client = http == null ? new BingClient(root) : new BingClient(root, http);
        var all = new List<Photo>();
        foreach (var mkt in markets)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var photos = await client.FetchUnderFeedLockAsync(mkt, resolution, token, allowOffline);
                UsedOfflineFallback |= client.UsedOfflineFallback;
                foreach (var p in photos)
                {
                    p.Source = "BingGlobal";
                    p.Copyright = (string.IsNullOrWhiteSpace(p.Copyright) ? "" : p.Copyright + " • ") + $"ریجن بین‌المللی {mkt}";
                }
                all.AddRange(photos);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { UsedOfflineFallback = true; Store.Log($"BingGlobal {mkt}: {ex.Message}", root); }
        }
        var distinct = all.DistinctBy(p => p.Id).ToList();
        if (distinct.Count == 0) throw new InvalidOperationException("دریافت تصاویر بین‌المللی بینگ با خطا مواجه شد.");
        MergeArchive(distinct);
        return distinct;
    }

    public async Task<List<Photo>> FetchIranNatureAsync(CancellationToken token = default, bool allowOffline = true)
    {
        await using var gate = await Store.AcquireLockAsync(Path.Combine(root, "feed.lock"), 10, token);
        var topics = new (string Query, string Title)[]
        {
            ("Damavand", "قله دماوند"), ("Persepolis", "تخت جمشید"),
            ("Kandovan Iran", "روستای کندوان"), ("Lut Desert", "کویر لوت"),
            ("Masouleh", "ماسوله"), ("Khaju Bridge", "پل خواجو"),
            ("Stars Valley Qeshm", "دره ستارگان قشم"), ("Hyrcanian forest -map -ecoregion -diagram", "جنگل هیرکانی")
        };
        var photos = new List<Photo>();
        var errors = new List<string>();
        foreach (var topic in topics)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var url = CommonsImageFeed(topic.Query);
                var bytes = http == null ? await SourceHttp.ReadAsync(url, 20_000_000, token) : await SourceHttp.ReadAsync(url, 20_000_000, token, http);
                var candidates = ParseCommonsImages(bytes, "IranNature", topic.Title);
                Photo? selected = null;
                foreach (var photo in candidates)
                {
                    try { await DownloadAsync(photo, token); selected = photo; break; }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception ex) when (SourceHttp.IsRecoverable(ex, token)) { errors.Add(topic.Query + ": " + ex.Message); }
                }
                if (selected == null) throw new InvalidDataException("Wikimedia returned no downloadable photo for " + topic.Query);
                photos.Add(selected);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (SourceHttp.IsRecoverable(ex, token))
            {
                UsedOfflineFallback = true;
                errors.Add(topic.Query + ": " + ex.Message);
                Store.Log("IranNature: " + topic.Query + ": " + ex.Message, root);
            }
        }
        if (photos.Count > 0) { MergeArchive(photos.DistinctBy(p => p.Id).ToList()); return photos; }
        var existing = Store.Read(Path.Combine(root, "archive.json"), new List<Photo>()).Where(p => p.Source == "IranNature" && IsHealthy(p)).ToList();
        if (allowOffline && existing.Count > 0) { UsedOfflineFallback = true; return existing; }
        throw new InvalidOperationException("دریافت تصاویر ایران از Wikimedia ناموفق بود: " + string.Join("; ", errors.Take(3)));
    }

    public static List<Photo> ParseCommonsImages(byte[] bytes, string source, string? topicTitle = null)
    {
        using var json = JsonDocument.Parse(bytes);
        if (json.RootElement.TryGetProperty("error", out var error)) throw new InvalidDataException("Wikimedia API: " + error.ToString());
        var photos = new List<Photo>();
        if (!json.RootElement.TryGetProperty("query", out var query) || !query.TryGetProperty("pages", out var pages)) return photos;
        foreach (var page in pages.EnumerateObject().Select(p => p.Value).OrderBy(p => p.TryGetProperty("index", out var index) ? index.GetInt32() : int.MaxValue))
        {
            if (!page.TryGetProperty("imageinfo", out var images) || images.GetArrayLength() == 0) continue;
            var info = images[0];
            if (!info.TryGetProperty("mime", out var mime) || mime.GetString() is not ("image/jpeg" or "image/png")) continue;
            if (!info.TryGetProperty("width", out var width) || width.GetInt32() < (source == "IranNature" ? 1000 : 600) || !info.TryGetProperty("height", out var height) || height.GetInt32() < 600) continue;
            var url = info.TryGetProperty("thumburl", out var thumb) ? thumb.GetString() : info.GetProperty("url").GetString();
            SourceHttp.Validate(url!);
            var title = page.GetProperty("title").GetString() ?? "Wikimedia";
            string Metadata(string name)
            {
                if (!info.TryGetProperty("extmetadata", out var meta) || !meta.TryGetProperty(name, out var property) || !property.TryGetProperty("value", out var value)) return "";
                return WebUtility.HtmlDecode(Regex.Replace(value.GetString() ?? "", "<[^>]*>", " ")).Trim();
            }
            photos.Add(new Photo { Source = source, Url = url!, Title = topicTitle ?? title.Replace("File:", ""),
                Date = DateTime.UtcNow.ToString("yyyyMMdd"), SourcePage = "https://commons.wikimedia.org/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_')),
                Copyright = string.Join(" • ", new[] { Metadata("Artist"), Metadata("LicenseShortName"), "Wikimedia Commons" }.Where(v => !string.IsNullOrWhiteSpace(v))) });
        }
        return photos;
    }

    public async Task<int> SyncAllOnlineSourcesAsync(string resolution = "UHD", CancellationToken token = default)
    {
        var totalSynced = 0;
        var failures = new List<string>();
        foreach (var sourceId in OnlineSourceIds)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var req = new SourceRequest(sourceId, "en-US", resolution, "");
                var sourceCatalog = http == null ? new SourceCatalog(root) : new SourceCatalog(root, http);
                var photos = await sourceCatalog.FetchAsync(req, token, allowOffline: false);
                totalSynced += photos.Count;
                if (sourceCatalog.UsedOfflineFallback) throw new InvalidOperationException("Online source refresh was partial; retry is required.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                failures.Add(sourceId + ": " + ex.Message);
                Store.Log($"Server sync warning for {sourceId}: {ex.Message}", root);
            }
        }
        PurgeDuplicates(root);
        if (failures.Count > 0) throw new InvalidOperationException($"Server sync incomplete ({totalSynced} image records refreshed): " + string.Join("; ", failures));
        return totalSynced;
    }
    public static IReadOnlyList<string> OnlineSourceIds => Options.Where(o => o.Id is not ("Folder" or "Favorites" or "Spotlight" or "SharedNetwork")).Select(o => o.Id).ToArray();

    public async Task<List<Photo>> FetchSharedNetworkAsync(string sharePath, CancellationToken token = default)
    {
        await using var gate = await Store.AcquireLockAsync(Path.Combine(root, "feed.lock"), 10, token);
        if (string.IsNullOrWhiteSpace(sharePath) || !Directory.Exists(sharePath))
            throw new DirectoryNotFoundException("مسیر مخزن اشتراکی سرور در دسترس نیست یا تعریف نشده است: " + sharePath);

        var serverArchive = Path.Combine(sharePath, "archive.json");
        var photos = new List<Photo>();

        if (File.Exists(serverArchive))
        {
            var serverPhotos = Store.Read(serverArchive, new List<Photo>());
            var localImgDir = Path.Combine(root, "Images");
            Directory.CreateDirectory(localImgDir);

            foreach (var sp in serverPhotos.Take(30))
            {
                token.ThrowIfCancellationRequested();
                var sourceFile = Path.Combine(sharePath, "Images", Path.GetFileName(sp.FilePath));
                if (!File.Exists(sourceFile)) sourceFile = Path.Combine(sharePath, Path.GetFileName(sp.FilePath));
                if (!File.Exists(sourceFile)) continue;
                if (!Store.IsOwnedFile(sourceFile, Path.GetDirectoryName(sourceFile)!) || !IsHealthy(new Photo { FilePath = sourceFile })) continue;

                var localTarget = Path.Combine(localImgDir, Path.GetFileName(sourceFile));
                var srcInfo = new FileInfo(sourceFile);
                if (srcInfo.Length > 150_000_000 || !Store.IsOwnedFile(localTarget, localImgDir)) continue;
                if (!File.Exists(localTarget) || new FileInfo(localTarget).Length != srcInfo.Length || new FileInfo(localTarget).LastWriteTimeUtc != srcInfo.LastWriteTimeUtc)
                {
                    await CopySharedImageAsync(sourceFile, localTarget, token);
                }

                photos.Add(new Photo
                {
                    Id = sp.Id,
                    Source = "SharedNetwork",
                    Title = sp.Title,
                    Copyright = (string.IsNullOrWhiteSpace(sp.Copyright) ? "" : sp.Copyright + " • ") + "سرور شبکه محلی",
                    Date = sp.Date,
                    Url = sp.Url,
                    Market = sp.Market,
                    FilePath = localTarget,
                    SourcePage = sp.SourcePage
                });
            }
        }
        else
        {
            // If server just contains raw image files in a folder
            photos = await Task.Run(() => ImportFolder(sharePath, token), token);
            foreach (var p in photos) p.Source = "SharedNetwork";
        }

        if (photos.Count == 0) throw new InvalidOperationException("هیچ تصویر معتبری در مخزن اشتراکی سرور یافت نشد.");
        MergeArchive(photos);
        return photos;
    }

    internal static async Task CopySharedImageAsync(string source, string target, CancellationToken token, Action<string, string>? copy = null)
    {
        var modified = File.GetLastWriteTimeUtc(source);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await Task.Run(() => { if (copy == null) File.Copy(source, temp); else copy(source, temp); }, token);
            Store.LoadImage(temp, 32);
            token.ThrowIfCancellationRequested();
            File.SetLastWriteTimeUtc(temp, modified);
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static List<Photo> ParseNasaFeed(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 25_000_000 });
        var xml = XDocument.Load(reader); var result = new List<Photo>();
        foreach (var item in xml.Descendants("item"))
        {
            var enclosure = item.Element("enclosure"); var url = enclosure?.Attribute("url")?.Value;
            if (url == null || enclosure?.Attribute("type")?.Value is not ("image/jpeg" or "image/png")) continue;
            if (long.TryParse(enclosure.Attribute("length")?.Value, out var length) && length > 50_000_000) continue;
            SourceHttp.Validate(url);
            var date = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed.ToString("yyyyMMdd") : "";
            result.Add(new Photo { Source = "NasaDaily", Title = WebUtility.HtmlDecode(item.Element("title")?.Value ?? "NASA"), Url = url,
                Date = date, SourcePage = item.Element("link")?.Value ?? "", Copyright = "NASA Image of the Day — اعتبار عکاس و شرایط استفاده در صفحه اصلی تصویر" });
        }
        return result.OrderByDescending(p => p.Date).ToList();
    }

    public static List<Photo> ParseNasaLibrary(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes); var result = new List<Photo>();
        foreach (var item in json.RootElement.GetProperty("collection").GetProperty("items").EnumerateArray())
        {
            if (!item.TryGetProperty("links", out var links)) continue;
            var data = item.GetProperty("data")[0];
            var image = links.EnumerateArray().Where(l => l.TryGetProperty("render", out var render) && render.GetString() == "image")
                .Where(l => l.TryGetProperty("href", out var href) && (href.GetString()!.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || href.GetString()!.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                .Where(l => !l.TryGetProperty("size", out var size) || size.GetInt64() <= 150_000_000)
                .OrderByDescending(l => l.TryGetProperty("width", out var width) ? width.GetInt32() : 0).FirstOrDefault();
            if (image.ValueKind == JsonValueKind.Undefined) continue;
            var url = image.GetProperty("href").GetString()!; SourceHttp.Validate(url);
            result.Add(new Photo { Source = "NasaLibrary", Url = url, Title = data.GetProperty("title").GetString() ?? "NASA",
                SourcePage = "https://images.nasa.gov/details/" + Uri.EscapeDataString(data.GetProperty("nasa_id").GetString()!),
                Date = DateTimeOffset.TryParse(data.GetProperty("date_created").GetString(), out var date) ? date.ToString("yyyyMMdd") : "",
                Copyright = data.TryGetProperty("secondary_creator", out var creator) ? creator.GetString() ?? "NASA" : "NASA — see original page for credits" });
        }
        return result;
    }

    public static List<Photo> ParseWikimediaFeed(byte[] bytes, string sourceName = "WikimediaPotd")
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 25_000_000 });
        var xml = XDocument.Load(reader); var result = new List<Photo>();
        var thumbRegex = new Regex(@"https://(?:thumb|upload)\.wikimedia\.org/wikipedia/commons/(?:thumb/)?([0-9a-f]/[0-9a-f]{2})/([^/""\s\?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var item in xml.Descendants("item"))
        {
            var desc = item.Element("description")?.Value ?? "";
            var match = thumbRegex.Match(desc);
            if (!match.Success) continue;

            var hash = match.Groups[1].Value;
            var fileName = match.Groups[2].Value;
            var fullUrl = $"https://upload.wikimedia.org/wikipedia/commons/{hash}/{fileName}";
            SourceHttp.Validate(fullUrl);

            var titleRaw = item.Element("title")?.Value ?? (sourceName == "NatGeoNature" ? "شگفتی‌های طبیعت و حیات‌وحش" : "Wikimedia Commons POTD");
            var date = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed.ToString("yyyyMMdd") : "";

            // Extract description plain text
            var descClean = Regex.Replace(desc, "<.*?>", " ").Trim();
            descClean = Regex.Replace(descClean, @"\s+", " ");
            if (descClean.Length > 120) descClean = descClean[..120] + "…";

            result.Add(new Photo
            {
                Source = sourceName,
                Title = WebUtility.HtmlDecode(titleRaw),
                Url = fullUrl,
                Date = date,
                SourcePage = item.Element("link")?.Value ?? "https://commons.wikimedia.org/wiki/Commons:Featured_pictures",
                Copyright = string.IsNullOrWhiteSpace(descClean) ? "برگزیده عکاسی بین‌المللی (CC / Public Domain)" : $"{descClean} — CC / Public Domain"
            });
        }
        return result.OrderByDescending(p => p.Date).ToList();
    }

    public static List<Photo> ParseWallhaven(byte[] bytes, string sourceName = "Wallhaven")
    {
        using var json = JsonDocument.Parse(bytes); var result = new List<Photo>();
        if (json.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var path = item.TryGetProperty("path", out var p) ? p.GetString() : null;
                if (string.IsNullOrWhiteSpace(path)) continue;
                SourceHttp.Validate(path);
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                var category = item.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "Wallhaven" : "Wallhaven";
                var res = item.TryGetProperty("resolution", out var resProp) ? resProp.GetString() ?? "4K" : "4K";
                var page = item.TryGetProperty("url", out var u) ? u.GetString() ?? "https://wallhaven.cc" : "https://wallhaven.cc";
                var date = item.TryGetProperty("created_at", out var cr) && DateTimeOffset.TryParse(cr.GetString(), out var dt) ? dt.ToString("yyyyMMdd") : "";
                var title = sourceName switch
                {
                    "CyberpunkArt" => $"هنر دیجیتال و سایبرپانک ({res})",
                    "Architecture4K" => $"معماری و منظره شهری ({res})",
                    _ => $"والپیپر برگزیده Wallhaven ({res} · {category})"
                };
                result.Add(new Photo
                {
                    Source = sourceName,
                    Id = sourceName + "-" + id,
                    Url = path,
                    Title = title,
                    SourcePage = page,
                    Date = date,
                    Copyright = $"Wallhaven Community · {res}"
                });
            }
        }
        return result;
    }

    public static List<Photo> ParseMuseumArt(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes); var result = new List<Photo>();
        if (json.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var imageId = item.TryGetProperty("image_id", out var imgId) ? imgId.GetString() : null;
                if (string.IsNullOrWhiteSpace(imageId)) continue;
                var url = $"https://www.artic.edu/iiif/2/{imageId}/full/1686,/0/default.jpg";
                SourceHttp.Validate(url);
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32().ToString() : imageId;
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "شاهکار هنری" : "شاهکار هنری";
                var artist = item.TryGetProperty("artist_title", out var a) ? a.GetString() ?? "هنرمند نامشخص" : "هنرمند نامشخص";
                var period = item.TryGetProperty("date_display", out var d) ? d.GetString() ?? "" : "";
                result.Add(new Photo
                {
                    Source = "MuseumArt",
                    Id = "MuseumArt-" + id,
                    Url = url,
                    Title = $"{title} — اثر {artist}",
                    SourcePage = $"https://www.artic.edu/artworks/{id}",
                    Date = DateTime.Now.ToString("yyyyMMdd"),
                    Copyright = $"موزه هنر شیکاگو (Art Institute) · {artist} ({period}) — Public Domain"
                });
            }
        }
        return result;
    }

    public static List<Photo> ParseEsaFeed(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 25_000_000 });
        var xml = XDocument.Load(reader); var result = new List<Photo>();
        foreach (var item in xml.Descendants("item"))
        {
            var enclosure = item.Element("enclosure");
            var url = enclosure?.Attribute("url")?.Value;
            if (string.IsNullOrEmpty(url)) continue;
            SourceHttp.Validate(url);

            var title = WebUtility.HtmlDecode(item.Element("title")?.Value ?? "ESA / Hubble");
            var date = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed.ToString("yyyyMMdd") : "";

            result.Add(new Photo
            {
                Source = "EsaHubble",
                Title = title,
                Url = url,
                Date = date,
                SourcePage = item.Element("link")?.Value ?? "https://esahubble.org/",
                Copyright = "ESA/Hubble & NASA Space Telescopes — تصویر نجومی هفته"
            });
        }
        return result.OrderByDescending(p => p.Date).ToList();
    }

    public static List<Photo> ParsePicsum(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes);
        var result = new List<Photo>();
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp)) continue;
            var id = idProp.GetString()!;
            var author = item.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "Unsplash" : "Unsplash";
            var origUrl = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "https://unsplash.com" : "https://unsplash.com";
            var downloadUrl = $"https://picsum.photos/id/{id}/3840/2160.jpg";
            SourceHttp.Validate(downloadUrl);
            result.Add(new Photo
            {
                Source = "UnsplashNature",
                Title = $"منظره و عکس هنری اثر {author}",
                Url = downloadUrl,
                Date = date,
                SourcePage = origUrl,
                Copyright = $"عکاسی منتخب Unsplash / اثر {author} (مجوز آزاد عکاسی Unsplash)"
            });
        }
        return result;
    }

    public static List<Photo> ParseUsgsFeed(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 25_000_000 });
        var xml = XDocument.Load(reader);
        var result = new List<Photo>();
        foreach (var item in xml.Descendants("item"))
        {
            var enclosure = item.Element("enclosure");
            var url = enclosure?.Attribute("url")?.Value;
            if (string.IsNullOrEmpty(url))
            {
                var media = item.Elements().FirstOrDefault(e => e.Name.LocalName is "content" or "thumbnail" && e.Attribute("url") != null);
                url = media?.Attribute("url")?.Value;
            }
            if (string.IsNullOrEmpty(url)) continue;
            SourceHttp.Validate(url);

            var title = WebUtility.HtmlDecode(item.Element("title")?.Value ?? "USGS Earth as Art");
            var date = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed.ToString("yyyyMMdd") : "";

            result.Add(new Photo
            {
                Source = "UsgsEarthArt",
                Title = title,
                Url = url,
                Date = date,
                SourcePage = item.Element("link")?.Value ?? "https://eros.usgs.gov/earth-as-art",
                Copyright = "سازمان زمین‌شناسی آمریکا (USGS Landsat Earth as Art) — تصویر ماهواره‌ای زمین"
            });
        }
        return result.OrderByDescending(p => p.Date).ToList();
    }

    async Task DownloadAsync(Photo photo, CancellationToken token)
    {
        photo.Id = photo.Source + "-" + Hash(photo.Url);
        photo.FilePath = Path.Combine(root, "Images", photo.Id + ".jpg");

        // 1. Check if the exact target file exists and is decodable
        if (File.Exists(photo.FilePath) && new FileInfo(photo.FilePath).Length > 1000)
        {
            try { Store.LoadImage(photo.FilePath, 32); return; }
            catch { try { File.Delete(photo.FilePath); } catch { } }
        }

        // 2. Check if another entry in archive has the same URL and valid file on disk
        var archivePath = Path.Combine(root, "archive.json");
        if (File.Exists(archivePath))
        {
            try
            {
                var archive = Store.Read(archivePath, new List<Photo>());
                var existing = archive.FirstOrDefault(p => (p.Id == photo.Id || p.Url == photo.Url) && File.Exists(p.FilePath) && new FileInfo(p.FilePath).Length > 1000);
                if (existing != null)
                {
                    Store.LoadImage(existing.FilePath, 32);
                    photo.FilePath = existing.FilePath;
                    return; // Reused existing valid file without re-downloading
                }
            }
            catch { }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(photo.FilePath)!);
        var temp = photo.FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (http == null) await SourceHttp.DownloadToFileAsync(photo.Url, temp, 150_000_000, token);
            else await SourceHttp.DownloadToFileAsync(photo.Url, temp, 150_000_000, token, http);
            ConvertToJpeg(temp, photo.FilePath);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    List<Photo> ImportFolder(string folder, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) throw new DirectoryNotFoundException("پوشه تصاویر انتخاب نشده یا در دسترس نیست.");
        var result = new List<Photo>();
        // Top-level only: do not walk junctions or recursively scan large disks.
        foreach (var file in Directory.EnumerateFiles(folder).Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(Path.GetExtension(f).ToLowerInvariant())).Order(StringComparer.Ordinal).Take(500))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file); if (info.Length > 30_000_000 || info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                var id = "Folder-" + Hash(Path.GetFullPath(file) + info.LastWriteTimeUtc.Ticks + info.Length);
                var destination = Path.Combine(root, "Images", id + ".jpg");
                if (!File.Exists(destination)) ConvertToJpeg(file, destination);
                else Store.LoadImage(destination, 32);
                result.Add(new Photo { Id = id, Source = "Folder", FilePath = destination, Title = Path.GetFileNameWithoutExtension(file), Date = info.LastWriteTime.ToString("yyyyMMdd"), Copyright = "تصویر انتخاب‌شده از پوشه شخصی" });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException) { Store.Log("Skipped local image: " + ex.Message, root); }
        }
        return result;
    }

    internal static void ConvertToJpeg(string input, string output)
    {
        using var inStream = File.OpenRead(input);
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(inStream, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) throw new InvalidDataException("تصویر انتخابی فاقد فریم معتبر است.");
        var frame = decoder.Frames[0];

        System.Windows.Media.Imaging.BitmapSource source = frame;
        var maxDim = Math.Max(frame.PixelWidth, frame.PixelHeight);
        if (maxDim > 3840)
        {
            var scale = 3840d / maxDim;
            source = new System.Windows.Media.Imaging.TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 95 };
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
            using (var outStream = File.Create(temp))
            {
                encoder.Save(outStream);
            }
            File.Move(temp, output, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static int PurgeDuplicates(string? root = null)
    {
        root ??= Store.Root;
        using var feedGate = Store.AcquireLockAsync(Path.Combine(root, "feed.lock")).GetAwaiter().GetResult();
        using var archiveGate = Store.AcquireLockAsync(Path.Combine(root, "archive.lock")).GetAwaiter().GetResult();
        return PurgeDuplicatesCore(root);
    }
    internal static int PurgeDuplicatesCore(string root)
    {
        var imgDir = Path.Combine(root, "Images");
        if (!Directory.Exists(imgDir)) return 0;

        var archivePath = Path.Combine(root, "archive.json");
        var archive = File.Exists(archivePath) ? Store.Read(archivePath, new List<Photo>()) : new List<Photo>();

        var hashToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var deletedCount = 0;

        var currentPath = Store.CurrentDesktopPhoto(root)?.FilePath;
        var files = Directory.EnumerateFiles(imgDir, "*.jpg").Where(f => Store.IsOwnedFile(f, imgDir)).Select(f => new FileInfo(f)).Where(f => f.Length > 0)
            .OrderBy(f => string.Equals(f.FullName, currentPath, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
        var sizeGroups = files.GroupBy(f => f.Length).Where(g => g.Count() > 1);

        foreach (var group in sizeGroups)
        {
            foreach (var file in group)
            {
                try
                {
                    string sha;
                    using (var stream = File.OpenRead(file.FullName))
                    {
                        sha = Convert.ToHexString(SHA256.HashData(stream));
                    }

                    if (hashToCanonical.TryGetValue(sha, out var canonicalPath) && File.Exists(canonicalPath))
                    {
                        if (!string.Equals(file.FullName, canonicalPath, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var p in archive.Where(p => string.Equals(p.FilePath, file.FullName, StringComparison.OrdinalIgnoreCase)))
                            {
                                p.FilePath = canonicalPath;
                            }
                            File.Delete(file.FullName);
                            deletedCount++;
                        }
                    }
                    else
                    {
                        hashToCanonical[sha] = file.FullName;
                    }
                }
                catch { }
            }
        }

        if (deletedCount > 0)
        {
            Store.Write(archivePath, archive.DistinctBy(p => p.Id).ToList());
            Store.Log($"Purged {deletedCount} duplicate image files.", root);
        }
        return deletedCount;
    }

    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24];

    void MergeArchive(List<Photo> photos)
    {
        Store.MergeArchive(root, photos);
    }
}
