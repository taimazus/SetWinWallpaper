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

namespace BingWallpaperPro;

public sealed record SourceOption(string Id, string Name);
public sealed record SourceRequest(string Id, string Market, string Resolution, string Folder, string NetworkShare = "");

public static class PhotoSelection
{
    public static SourceRequest Request(Preferences settings, bool lockScreen)
    {
        if (settings.Mode is not ("Same" or "Previous" or "Regions")) throw new InvalidDataException("حالت انتخاب تصویر ناشناخته است.");
        var independent = lockScreen && settings.Mode == "Regions";
        return new SourceRequest(
            independent ? settings.LockSource : settings.DesktopSource,
            independent ? settings.LockMarket : settings.Market,
            settings.Resolution,
            independent ? settings.LockFolder : settings.DesktopFolder,
            independent ? settings.LockNetworkSharePath : settings.NetworkSharePath);
    }
    public static Photo Select(IReadOnlyList<Photo> photos, string source, bool previous, DateOnly day)
    {
        if (photos.Count == 0) throw new InvalidOperationException("منبع انتخاب‌شده تصویر قابل استفاده ندارد.");
        if (previous && photos.Count < 2) throw new InvalidOperationException("برای تصویر قبلی، حداقل دو تصویر در منبع لازم است.");
        // Daily feeds use newest-first. Collections rotate deterministically each local calendar day.
        var index = source is "Bing" or "NasaDaily" or "WikimediaPotd" or "EsaHubble" ? 0 : day.DayNumber % photos.Count;
        return photos[(index + (previous ? 1 : 0)) % photos.Count];
    }
}

public static partial class SourceHttp
{
    static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 }) { Timeout = Timeout.InfiniteTimeSpan };
    static SourceHttp() => Client.DefaultRequestHeaders.UserAgent.ParseAdd("BingWallpaperPro/1.4 (Windows desktop wallpaper manager; +https://irres.ir)");

    public static Uri Validate(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            throw new InvalidDataException("نشانی منبع در فهرست میزبان‌های مجاز HTTPS نیست.");

        var host = uri.Host.ToLowerInvariant();
        var isAllowed = host is "www.bing.com" or "www.nasa.gov" or "images-api.nasa.gov" or "images-assets.nasa.gov"
            or "commons.wikimedia.org" or "upload.wikimedia.org" or "thumb.wikimedia.org"
            or "esahubble.org" or "cdn.esahubble.org" or "www.eso.org" or "cdn.eso.org"
            or "picsum.photos" or "fastly.picsum.photos" or "images.unsplash.com"
            or "eros.usgs.gov" or "landsat.usgs.gov" or "pubs.usgs.gov" or "earthexplorer.usgs.gov";

        if (!isAllowed)
            throw new InvalidDataException("میزبان منبع مجاز نیست: " + uri.Host);

        return uri;
    }

    public static async Task<byte[]> ReadAsync(string url, int limit = 20_000_000, CancellationToken cancellation = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        using var response = await Client.GetAsync(Validate(url), HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
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

    public static async Task DownloadToFileAsync(string url, string targetPath, long limit = 150_000_000, CancellationToken cancellation = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        var token = deadline.Token;
        using var response = await Client.GetAsync(Validate(url), HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
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
    public static readonly SourceOption[] Options =
    [
        new("Bing", "Microsoft Bing — تصویر روز"),
        new("BingGlobal", "گلچین بین‌المللی بینگ (تمام قاره‌ها و کشورها)"),
        new("IranNature", "ایران زیبا — طبیعت، کوهستان‌ها و میراث باستانی ایران"),
        new("UnsplashNature", "Unsplash & Picsum — عکاسی طبیعت و مناظر 4K"),
        new("WikimediaPotd", "Wikimedia Commons — تصویر منتخب روز"),
        new("Spotlight", "Microsoft Spotlight — کش محلی"),
        new("UsgsEarthArt", "USGS Earth as Art — شگفتی‌های زمین از فضا"),
        new("NasaDaily", "NASA — تصویر نجومی روز (APOD)"),
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

    readonly string root;
    public SourceCatalog(string? root = null) => this.root = root ?? Store.Root;

    public async Task<List<Photo>> FetchAsync(SourceRequest request, CancellationToken token = default)
    {
        if (!Options.Any(o => o.Id == request.Id)) throw new ArgumentException("منبع ناشناخته است: " + request.Id);
        if (request.Id == "Bing") return await new BingClient(root).FetchAsync(request.Market, request.Resolution, token);
        if (request.Id == "BingGlobal") return await FetchBingGlobalAsync(request.Resolution, token);
        if (request.Id == "IranNature") return await FetchIranNatureAsync(token);
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
        await using var gate = new FileStream(Path.Combine(root, "feed.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        List<Photo> photos;
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
                _ => throw new ArgumentException("منبع آنلاین ناشناخته: " + request.Id)
            };

            var bytes = await SourceHttp.ReadAsync(url, 20_000_000, token);
            var candidates = request.Id switch
            {
                "NasaDaily" => ParseNasaFeed(bytes),
                "NasaLibrary" => ParseNasaLibrary(bytes),
                "WikimediaPotd" => ParseWikimediaFeed(bytes),
                "EsaHubble" => ParseEsaFeed(bytes),
                "UnsplashNature" => ParsePicsum(bytes),
                "UsgsEarthArt" => ParseUsgsFeed(bytes),
                _ => []
            };

            photos = [];
            var takeCount = request.Id is "NasaDaily" or "WikimediaPotd" or "EsaHubble" ? 6 : 15;
            foreach (var photo in candidates.Take(takeCount))
            {
                token.ThrowIfCancellationRequested();
                try { await DownloadAsync(photo, token); photos.Add(photo); }
                catch (Exception ex) when (ex is HttpRequestException or IOException or NotSupportedException or ArgumentException || ex is OperationCanceledException && !token.IsCancellationRequested)
                { Store.Log($"Skipped {request.Id} image {photo.Url}: {ex.Message}", root); }
            }
        }
        if (photos.Count == 0)
        {
            var fallback = Store.Read(Path.Combine(root, "archive.json"), new List<Photo>())
                .Where(p => (string.Equals(p.Source, request.Id, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(p.Source)) && File.Exists(p.FilePath) && new FileInfo(p.FilePath).Length > 1000)
                .ToList();
            if (fallback.Count > 0)
            {
                Store.Log($"عدم دریافت تصویر جدید از منبع {request.Id}؛ استفاده خودکار از {fallback.Count} تصویر موجود در آرشیو محلی.", root);
                return fallback;
            }
            throw new InvalidOperationException("منبع تصویر سالمی برنگرداند؛ گزارش اجراها و اتصال شبکه را بررسی کنید.");
        }
        MergeArchive(photos); return photos;
    }

    public async Task<List<Photo>> FetchBingGlobalAsync(string resolution, CancellationToken token = default)
    {
        var markets = new[] { "en-US", "ja-JP", "de-DE", "fr-FR", "en-GB", "zh-CN", "pt-BR", "it-IT", "es-ES", "en-CA", "en-AU", "en-IN" };
        var client = new BingClient(root);
        var all = new List<Photo>();
        foreach (var mkt in markets)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var photos = await client.FetchAsync(mkt, resolution, token);
                foreach (var p in photos)
                {
                    p.Source = "BingGlobal";
                    p.Copyright = (string.IsNullOrWhiteSpace(p.Copyright) ? "" : p.Copyright + " • ") + $"ریجن بین‌المللی {mkt}";
                }
                all.AddRange(photos);
            }
            catch { }
        }
        var distinct = all.DistinctBy(p => p.Id).ToList();
        if (distinct.Count == 0) throw new InvalidOperationException("دریافت تصاویر بین‌المللی بینگ با خطا مواجه شد.");
        MergeArchive(distinct);
        return distinct;
    }

    public async Task<List<Photo>> FetchIranNatureAsync(CancellationToken token = default)
    {
        var curated = new List<(string Id, string Title, string Url, string Credit, string Page)>
        {
            ("iran-damavand", "قله دماوند — بام ایران و بلندترین قله آتشفشانی آسیا", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/Mount_Damavand_in_winter.jpg/1920px-Mount_Damavand_in_winter.jpg", "عکاسی از طبیعت البرز • مازندران و تهران", "https://fa.wikipedia.org/wiki/%D8%AF%D9%85%D8%A7%D9%88%D9%86%D8%AF"),
            ("iran-persepolis", "تخت جمشید (پارسه) — کاخ آپادانا و شکوه هخامنشیان", "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1d/Persepolis_Iran_2.jpg/1920px-Persepolis_Iran_2.jpg", "میراث جهانی یونسکو • استان فارس، شیراز", "https://fa.wikipedia.org/wiki/%D8%AA%D8%AE%D8%AA_%D8%AC%D9%85%D8%B4%DB%8C%D8%AF"),
            ("iran-sahand", "دامنه‌های کوهستان سهند و دره باستانی کندوان", "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a2/Kandovan_village_in_East_Azerbaijan_Iran.jpg/1920px-Kandovan_village_in_East_Azerbaijan_Iran.jpg", "طبیعت آذربایجان شرقی • رشته‌کوه سهند", "https://fa.wikipedia.org/wiki/%D8%B3%D9%87%D9%86%D8%AF"),
            ("iran-lut-desert", "کویر لوت و کلوت‌های افسانه‌ای شهداد", "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7b/Kaluts_in_Lut_Desert_Iran.jpg/1920px-Kaluts_in_Lut_Desert_Iran.jpg", "میراث طبیعی جهانی یونسکو • کرمان", "https://fa.wikipedia.org/wiki/%DA%A9%D9%88%DB%8C%D8%B1_%D9%84%D9%88%D8%AA"),
            ("iran-masouleh", "روستای تاریخی و پلکانی ماسوله در دل مه جنگل", "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b3/Masouleh_village_Gilan_Iran.jpg/1920px-Masouleh_village_Gilan_Iran.jpg", "معماری بومی و طبیعت گیلان", "https://fa.wikipedia.org/wiki/%D9%85%D8%A7%D8%B3%D9%88%D9%84%D9%87"),
            ("iran-isfahan-khaju", "پل خواجو و زاینده‌رود در شامگاه اصفهان", "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b8/Khaju_Bridge_Isfahan_at_Night.jpg/1920px-Khaju_Bridge_Isfahan_at_Night.jpg", "شاهکار معماری عصر صفوی • اصفهان", "https://fa.wikipedia.org/wiki/%D9%BE%D9%84_%D8%AE%D9%88%D8%A7%D8%AC%D9%88"),
            ("iran-qeshm-stars", "دره ستارگان — اشکال تماشایی فرسایشی ژئوپارک قشم", "https://upload.wikimedia.org/wikipedia/commons/thumb/9/95/Stars_Valley_Qeshm_Island_Iran.jpg/1920px-Stars_Valley_Qeshm_Island_Iran.jpg", "ژئوپارک جهانی قشم • خلیج فارس و هرمزگان", "https://fa.wikipedia.org/wiki/%D8%AF%D8%B1%D9%87_%D8%B3%D8%AA%D8%A7%D8%B1%DA%AF%D8%A7%D9%86"),
            ("iran-hyrcanian-forest", "جنگل‌های کهن هیرکانی و دریاچه مه‌آلود", "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6f/Hyrcanian_Forests_in_Mazandaran.jpg/1920px-Hyrcanian_Forests_in_Mazandaran.jpg", "میراث طبیعی جهانی یونسکو • نوار جنگلی شمال ایران", "https://fa.wikipedia.org/wiki/%D8%AC%D9%86%DA%AF%D9%84%E2%80%8C%D9%87%D8%A7%DB%8C_%D9%87%DB%8C%D8%B1%DA%A9%D8%A7%D9%86%DB%8C")
        };

        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var photos = new List<Photo>();

        foreach (var item in curated)
        {
            token.ThrowIfCancellationRequested();
            var photo = new Photo
            {
                Id = item.Id,
                Source = "IranNature",
                Title = item.Title,
                Url = item.Url,
                Date = date,
                SourcePage = item.Page,
                Copyright = item.Credit,
                FilePath = Path.Combine(root, "Images", item.Id + ".jpg")
            };

            try
            {
                await DownloadAsync(photo, token);
                photos.Add(photo);
            }
            catch (Exception ex)
            {
                Store.Log($"IranNature image download skipped ({item.Id}): {ex.Message}", root);
                if (File.Exists(photo.FilePath)) photos.Add(photo);
            }
        }

        if (photos.Count == 0)
        {
            var existing = Store.Read(Path.Combine(root, "archive.json"), new List<Photo>())
                .Where(p => p.Source == "IranNature" && File.Exists(p.FilePath)).ToList();
            if (existing.Count > 0) return existing;
            throw new InvalidOperationException("دریافت تصاویر طبیعت ایران با خطا مواجه شد.");
        }

        MergeArchive(photos);
        return photos;
    }

    public async Task<int> SyncAllOnlineSourcesAsync(string resolution = "UHD", CancellationToken token = default)
    {
        var onlineSources = new[] { "BingGlobal", "UnsplashNature", "WikimediaPotd", "UsgsEarthArt", "NasaDaily", "EsaHubble" };
        var totalSynced = 0;
        foreach (var sourceId in onlineSources)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var req = new SourceRequest(sourceId, "en-US", resolution, "");
                var photos = await FetchAsync(req, token);
                totalSynced += photos.Count;
            }
            catch (Exception ex)
            {
                Store.Log($"Server sync warning for {sourceId}: {ex.Message}", root);
            }
        }
        PurgeDuplicates(root);
        return totalSynced;
    }

    public async Task<List<Photo>> FetchSharedNetworkAsync(string sharePath, CancellationToken token = default)
    {
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
                var sourceFile = File.Exists(sp.FilePath) ? sp.FilePath : Path.Combine(sharePath, "Images", Path.GetFileName(sp.FilePath));
                if (!File.Exists(sourceFile)) sourceFile = Path.Combine(sharePath, Path.GetFileName(sp.FilePath));
                if (!File.Exists(sourceFile)) continue;

                var localTarget = Path.Combine(localImgDir, Path.GetFileName(sourceFile));
                if (!File.Exists(localTarget) || new FileInfo(localTarget).Length != new FileInfo(sourceFile).Length)
                {
                    await Task.Run(() => File.Copy(sourceFile, localTarget, true), token);
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

    public static List<Photo> ParseNasaFeed(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 25_000_000 });
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

    public static List<Photo> ParseWikimediaFeed(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 25_000_000 });
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

            var titleRaw = item.Element("title")?.Value ?? "Wikimedia Commons POTD";
            var date = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed.ToString("yyyyMMdd") : "";

            // Extract description plain text
            var descClean = Regex.Replace(desc, "<.*?>", " ").Trim();
            descClean = Regex.Replace(descClean, @"\s+", " ");
            if (descClean.Length > 120) descClean = descClean[..120] + "…";

            result.Add(new Photo
            {
                Source = "WikimediaPotd",
                Title = WebUtility.HtmlDecode(titleRaw),
                Url = fullUrl,
                Date = date,
                SourcePage = item.Element("link")?.Value ?? "https://commons.wikimedia.org/wiki/Commons:Picture_of_the_day",
                Copyright = string.IsNullOrWhiteSpace(descClean) ? "Wikimedia Commons Picture of the Day (CC / Public Domain)" : $"Wikimedia POTD: {descClean} — CC / Public Domain"
            });
        }
        return result.OrderByDescending(p => p.Date).ToList();
    }

    public static List<Photo> ParseEsaFeed(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 25_000_000 });
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
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 25_000_000 });
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
                    photo.Id = existing.Id;
                    return; // Reused existing valid file without re-downloading
                }
            }
            catch { }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(photo.FilePath)!);
        var temp = photo.FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await SourceHttp.DownloadToFileAsync(photo.Url, temp, 150_000_000, token);
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

    static void ConvertToJpeg(string input, string output)
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
        var imgDir = Path.Combine(root, "Images");
        if (!Directory.Exists(imgDir)) return 0;

        var archivePath = Path.Combine(root, "archive.json");
        var archive = File.Exists(archivePath) ? Store.Read(archivePath, new List<Photo>()) : new List<Photo>();

        var hashToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var deletedCount = 0;

        var files = Directory.EnumerateFiles(imgDir, "*.jpg").Select(f => new FileInfo(f)).Where(f => f.Length > 0).ToList();
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
        var path = Path.Combine(root, "archive.json");
        Store.Write(path, photos.Concat(Store.Read(path, new List<Photo>())).DistinctBy(p => p.Id).ToList());
    }
}
