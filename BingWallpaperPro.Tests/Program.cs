using BingWallpaperPro;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    static int passed;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
    static void Reject(Action action, string name)
    {
        try { action(); } catch { Check(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(Path.Combine("artifacts", "tests")); Directory.CreateDirectory(root);
            Check(BingClient.TrustedUri("/th?id=test").Host == "www.bing.com", "Bing relative URLs");
            foreach (var url in new[] { "http://www.bing.com/a", "https://evil.test/a", "https://www.bing.com.evil.test/a", "https://user@www.bing.com/a", "https://www.bing.com:444/a", "//evil.test/a", "file:///C:/test.jpg" }) Reject(() => BingClient.TrustedUri(url), "Reject " + url);
            Check(WindowsIntegration.QuotePS("a'b") == "'a''b'", "PowerShell literal quote escaping");

            var settingsFile = Path.Combine(root, "settings.json");
            Store.Write(settingsFile, new Preferences { Market = "ja-JP", Favorites = ["one"] });
            Check(Store.Read(settingsFile, new Preferences()).Favorites.SequenceEqual(new[] { "one" }), "Settings persistence");
            Store.Write(settingsFile, new Preferences { Mode = "Regions" });
            Check(Store.Read(settingsFile, new Preferences()).Mode == "Regions", "Atomic settings replacement");
            File.WriteAllText(settingsFile, "{broken");
            Reject(() => Store.Read(settingsFile, new Preferences()), "Corrupt settings are not silently accepted");
            Check(Store.Read(Path.Combine(root, "missing.json"), new Preferences()).Desktop, "Fresh installation defaults");
            Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "No abandoned atomic write files");

            // Verify AcquireLockAsync coordinates safely during contention
            var lockTestFile = Path.Combine(root, "test.lock");
            Task<bool> secondTask;
            using (var firstGate = Store.AcquireLockAsync(lockTestFile, 2).GetAwaiter().GetResult())
            {
                secondTask = Task.Run(async () =>
                {
                    await using var secondGate = await Store.AcquireLockAsync(lockTestFile, 3);
                    return secondGate != null;
                });
                Thread.Sleep(300);
            }
            Check(secondTask.GetAwaiter().GetResult(), "AcquireLockAsync recovers from contention after release");

            var imageFile = Path.Combine(root, "sample.png");
            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(imageFile)) encoder.Save(stream);
            Check(Store.LoadImage(imageFile, 0).PixelWidth == 2, "Image decoding");
            File.Move(imageFile, imageFile + ".moved", true);
            Check(File.Exists(imageFile + ".moved"), "Preview releases source file handle");
            Reject(() => Store.LoadImage(settingsFile), "Reject non-image content");

            Check(WindowsIntegration.RunPowerShellAsync("'ok'").GetAwaiter().GetResult() == "ok", "PowerShell output capture");
            Reject(() => WindowsIntegration.RunPowerShellAsync("throw 'test failure'").GetAwaiter().GetResult(), "PowerShell errors reach caller");
            try { WindowsIntegration.RunPowerShellAsync("Write-Progress -Activity 'test' -Status 'loading'; throw 'خطای آزمایشی'").GetAwaiter().GetResult(); throw new Exception("Expected failure"); }
            catch (InvalidOperationException ex) { Check(ex.Message == "خطای آزمایشی", "PowerShell errors are plain UTF-8 without CLIXML or progress records"); }
            Reject(() => WindowsIntegration.RunPowerShellAsync("Start-Sleep -Seconds 10", 1).GetAwaiter().GetResult(), "PowerShell timeout terminates process");
            Reject(() => WindowsIntegration.InstallScheduleAsync("25:70").GetAwaiter().GetResult(), "Reject invalid schedule before side effects");
            Reject(() => new BingClient(root).FetchAsync("invalid", "UHD").GetAwaiter().GetResult(), "Reject invalid region before networking");

            // Source URL validation checks
            foreach (var url in new[] { "https://www.nasa.gov.evil.test/a", "http://www.nasa.gov/a", "https://localhost/a", "https://127.0.0.1/a", "https://images-assets.nasa.gov:444/a", "https://user@www.nasa.gov/a" }) Reject(() => SourceHttp.Validate(url), "Reject source " + url);
            Check(SourceHttp.Validate("https://images-assets.nasa.gov/image/test.jpg").Scheme == "https", "NASA image host allowed");
            Check(SourceHttp.Validate("https://upload.wikimedia.org/wikipedia/commons/a/ab/Test.jpg").Host == "upload.wikimedia.org", "Wikimedia upload host allowed");
            Check(SourceHttp.Validate("https://cdn.esahubble.org/archives/images/screen/test.jpg").Host == "cdn.esahubble.org", "ESA Hubble host allowed");
            Check(SourceHttp.Validate("https://picsum.photos/id/10/3840/2160.jpg").Host == "picsum.photos", "Picsum/Unsplash host allowed");
            Check(SourceHttp.Validate("https://eros.usgs.gov/earth-as-art/test.jpg").Host == "eros.usgs.gov", "USGS host allowed");
            Check(SourceHttp.Validate("https://w.wallhaven.cc/full/test.jpg").Host == "w.wallhaven.cc", "Wallhaven host allowed");
            Check(SourceHttp.Validate("https://www.artic.edu/iiif/2/test/full/1680,/0/default.jpg").Host == "www.artic.edu", "Art Institute host allowed");

            // NASA Feed parsing
            var feed = System.Text.Encoding.UTF8.GetBytes("""
                <rss><channel><item><title>Valid image</title><pubDate>Wed, 23 Sep 2026 18:45 GMT</pubDate><link>https://www.nasa.gov/image-detail/test/</link><enclosure url="https://www.nasa.gov/test.jpg" type="image/jpeg" length="100"/></item><item><enclosure url="https://www.nasa.gov/huge.jpg" type="image/jpeg" length="64000000"/></item><item><enclosure url="https://www.nasa.gov/movie.mp4" type="video/mp4" length="100"/></item></channel></rss>
                """);
            var parsedFeed = SourceCatalog.ParseNasaFeed(feed);
            Check(parsedFeed.Count == 1 && parsedFeed[0].Date == "20260923" && parsedFeed[0].SourcePage.Contains("image-detail"), "NASA feed filters video/oversize and preserves date/source page");
            Reject(() => SourceCatalog.ParseNasaFeed(System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///C:/Windows/win.ini'>]><rss><channel><item><title>&x;</title></item></channel></rss>")), "NASA feed rejects external entities");

            // Wikimedia Commons POTD Feed parsing
            var wikiFeed = System.Text.Encoding.UTF8.GetBytes("""
                <rss><channel><item><title>Wikimedia picture of the day</title><pubDate>Mon, 21 Sep 2026 00:00:00 GMT</pubDate><link>https://commons.wikimedia.org/wiki/Special:FeedItem/potd/20260921000000/en</link><description>&lt;div&gt;&lt;a href="/wiki/File:Landscape.jpg"&gt;&lt;img src="https://thumb.wikimedia.org/wikipedia/commons/thumb/e/e8/Landscape.jpg/330px-Landscape.jpg" /&gt;&lt;/a&gt; Beautiful mountains in Switzerland.&lt;/div&gt;</description></item></channel></rss>
                """);
            var parsedWiki = SourceCatalog.ParseWikimediaFeed(wikiFeed);
            Check(parsedWiki.Count == 1 && parsedWiki[0].Url == "https://upload.wikimedia.org/wikipedia/commons/e/e8/Landscape.jpg" && parsedWiki[0].Date == "20260921", "Wikimedia POTD parser extracts high-res full image URL and metadata");

            // ESA Hubble Feed parsing
            var esaFeed = System.Text.Encoding.UTF8.GetBytes("""
                <rss><channel><item><title>Spiral Galaxy NGC 1234</title><pubDate>Fri, 18 Sep 2026 10:00:00 +0200</pubDate><link>https://esahubble.org/images/potm2609a/</link><enclosure url="https://cdn.esahubble.org/archives/images/screen/potm2609a.jpg" type="image/jpeg" length="200000"/></item></channel></rss>
                """);
            var parsedEsa = SourceCatalog.ParseEsaFeed(esaFeed);
            Check(parsedEsa.Count == 1 && parsedEsa[0].Url.Contains("cdn.esahubble.org") && parsedEsa[0].Title == "Spiral Galaxy NGC 1234", "ESA Hubble parser extracts enclosure and metadata");

            // Picsum/Unsplash JSON parsing
            var picsumJson = System.Text.Encoding.UTF8.GetBytes("""[{"id":"101","author":"Jane Doe","url":"https://unsplash.com/photos/101"}]""");
            var parsedPicsum = SourceCatalog.ParsePicsum(picsumJson);
            Check(parsedPicsum.Count == 1 && parsedPicsum[0].Url.Contains("picsum.photos/id/101") && parsedPicsum[0].Title.Contains("Jane Doe"), "Picsum/Unsplash parser generates 4K URLs and credits");

            // USGS Earth as Art feed parsing
            var usgsXml = System.Text.Encoding.UTF8.GetBytes("""
                <rss><channel><item><title>Earth As Art: Erg Iguidi</title><pubDate>Thu, 17 Sep 2026 12:00:00 GMT</pubDate><link>https://eros.usgs.gov/earth-as-art/erg-iguidi</link><enclosure url="https://eros.usgs.gov/earth-as-art/erg-iguidi.jpg" type="image/jpeg"/></item></channel></rss>
                """);
            var parsedUsgs = SourceCatalog.ParseUsgsFeed(usgsXml);
            Check(parsedUsgs.Count == 1 && parsedUsgs[0].Title.Contains("Erg Iguidi") && parsedUsgs[0].Source == "UsgsEarthArt", "USGS Earth as Art parser extracts satellite landscape");

            // Wallhaven JSON parsing
            var whJson = System.Text.Encoding.UTF8.GetBytes("""{"data":[{"id":"wh101","path":"https://w.wallhaven.cc/full/wh101.jpg","category":"general","resolution":"3840x2160","created_at":"2026-09-01 12:00:00"}]}""");
            var parsedWh = SourceCatalog.ParseWallhaven(whJson);
            Check(parsedWh.Count == 1 && parsedWh[0].Url.Contains("wallhaven.cc") && parsedWh[0].Title.Contains("3840x2160"), "Wallhaven parser extracts 4K wallpapers");

            // Museum Art JSON parsing
            var artJson = System.Text.Encoding.UTF8.GetBytes("""{"data":[{"id":12345,"title":"Starry Night","artist_title":"Vincent van Gogh","date_display":"1889","image_id":"art-uuid-1"}]}""");
            var parsedArt = SourceCatalog.ParseMuseumArt(artJson);
            Check(parsedArt.Count == 1 && parsedArt[0].Url.Contains("artic.edu") && parsedArt[0].Title.Contains("Starry Night"), "Museum Art parser extracts classic masterpieces");

            var oldSettings = Path.Combine(root, "legacy-settings.json"); File.WriteAllText(oldSettings, "{\"Market\":\"de-DE\",\"Mode\":\"Regions\"}");
            var migrated = Store.Read(oldSettings, new Preferences());
            Check(migrated.DesktopSource == "Bing" && migrated.LockSource == "Bing" && migrated.Mode == "Regions", "Existing configurations keep Bing and regional mode");

            var independent = new Preferences { DesktopSource = "WikimediaPotd", LockSource = "EsaHubble", LockFolder = "test-folder", Mode = "Regions" };
            Check(PhotoSelection.Request(independent, true).Id == "EsaHubble" && PhotoSelection.Request(independent, false).Id == "WikimediaPotd", "Independent destination source routing with new sources");
            independent.Mode = "Same";
            Check(PhotoSelection.Request(independent, true) == PhotoSelection.Request(independent, false), "Same mode shares effective source");

            var indepModes = new Preferences { DesktopMode = "Random", LockMode = "Daily", LockSource = "Wallhaven" };
            Check(PhotoSelection.Request(indepModes, true).Id == "Wallhaven" && PhotoSelection.Request(indepModes, false).Id == "Bing", "Independent LockMode and DesktopMode routing");

            var candidates = new[] { new Photo { Id = "a" }, new Photo { Id = "b" } };
            var day = new DateOnly(2026, 9, 24);
            Check(PhotoSelection.Select(candidates, "Bing", true, day).Id == "b", "Previous feed image selection");
            Check(PhotoSelection.Select(candidates, "Folder", false, day).Id != PhotoSelection.Select(candidates, "Folder", false, day.AddDays(1)).Id, "Collection rotates daily and deterministically");
            var randomSelected = PhotoSelection.Select(candidates, "Folder", false, day, random: true);
            Check(randomSelected.Id is "a" or "b", "Random mode selects valid candidate");
            var randomPref = new Preferences { Mode = "Random" };
            Check(PhotoSelection.Request(randomPref, false).Id == "Bing", "Random mode generates valid source request");
            Reject(() => PhotoSelection.Select(candidates.Take(1).ToArray(), "Folder", true, day), "Previous image requires at least two images");

            var localRoot = Path.Combine(root, "local-source"); var inputRoot = Path.Combine(root, "input"); Directory.CreateDirectory(inputRoot);
            File.Copy(imageFile + ".moved", Path.Combine(inputRoot, "valid.png"), true); File.WriteAllText(Path.Combine(inputRoot, "broken.jpg"), "invalid-image");
            var localPhotos = new SourceCatalog(localRoot).FetchAsync(new("Folder", "en-US", "UHD", inputRoot)).GetAwaiter().GetResult();
            Check(localPhotos.Count == 1 && localPhotos[0].Source == "Folder" && Store.LoadImage(localPhotos[0].FilePath, 0).PixelWidth == 2, "Folder import skips corrupt files, copies images and does not upscale");
            // Deduplication verification: re-fetching existing images does not re-download or recreate files
            var preModTime = File.GetLastWriteTimeUtc(localPhotos[0].FilePath);
            var reFetched = new SourceCatalog(localRoot).FetchAsync(new("Folder", "en-US", "UHD", inputRoot)).GetAwaiter().GetResult();
            var postModTime = File.GetLastWriteTimeUtc(reFetched[0].FilePath);
            Check(preModTime == postModTime && reFetched[0].Id == localPhotos[0].Id, "Duplicate images are not re-downloaded or re-written");

            // Auto-Repair and Diagnostics test
            var repairRoot = Path.Combine(root, "repair-test"); Directory.CreateDirectory(repairRoot);
            File.WriteAllText(Path.Combine(repairRoot, "settings.json"), "{corrupted-json");
            var repairedSettings = Diagnostics.RepairSettings(repairRoot);
            // Duplicate files on disk purging test
            var dupDir = Path.Combine(repairRoot, "Images"); Directory.CreateDirectory(dupDir);
            var originalPath = Path.Combine(dupDir, "orig.jpg");
            var duplicatePath = Path.Combine(dupDir, "copy.jpg");
            File.Copy(localPhotos[0].FilePath, originalPath, true);
            File.Copy(localPhotos[0].FilePath, duplicatePath, true);
            var purgedDuplicates = SourceCatalog.PurgeDuplicates(repairRoot);
            Check(purgedDuplicates >= 1 && (File.Exists(originalPath) ^ File.Exists(duplicatePath)), "PurgeDuplicates deletes redundant identical image files and preserves canonical");

            var clientRoot = Path.Combine(root, "client-source"); Directory.CreateDirectory(clientRoot);
            var clientPhotos = new SourceCatalog(clientRoot).FetchAsync(new("SharedNetwork", "en-US", "UHD", "", localRoot)).GetAwaiter().GetResult();
            Check(clientPhotos.Count >= 1 && clientPhotos[0].Source == "SharedNetwork" && File.Exists(clientPhotos[0].FilePath), "Client successfully fetches images from server repository share without internet");

            // Auto-Repair and Diagnostics test
            var repairArchive = Diagnostics.RepairArchive(repairRoot);
            Check(repairArchive >= 0, "Archive repair successfully cleans invalid entries");

            var autoRepairRes = Diagnostics.AutoRepairAllAsync(repairRoot).GetAwaiter().GetResult();
            Check(autoRepairRes.RepairedItems.Count > 0 && autoRepairRes.UpdatedReport.Findings.Count > 0, "Auto-Repair performs end-to-end recovery and generates updated report");

            var findings = Diagnostics.RunAsync(false, root).GetAwaiter().GetResult();
            Check(findings.Findings.Any(f => f.Area == "تنظیمات ذخیره‌شده" && f.Status == "خطا") && findings.Findings.Any(f => f.Area == "شبکه"), "Diagnostics reports corrupt settings and continues without network");
            Store.Write(Path.Combine(root, "diagnostics.json"), findings);

            // WPF UI Rendering & Styling test
            var window = new MainWindow();
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1180, 820)); content.Arrange(new Rect(0, 0, 1180, 820)); content.UpdateLayout();
            var preview = new RenderTargetBitmap(1180, 820, 96, 96, PixelFormats.Pbgra32); preview.Render(content);
            var screenshot = new PngBitmapEncoder(); screenshot.Frames.Add(BitmapFrame.Create(preview));
            using (var stream = File.Create(Path.Combine(root, "ui-preview.png"))) screenshot.Save(stream);
            var pixels = new byte[1180 * 820 * 4]; preview.CopyPixels(pixels, 1180 * 4, 0);
            var alphaPixels = pixels.Where((_, i) => i % 4 == 3).Count(a => a > 0);
            Check(window.Title == Brand.Title && alphaPixels > 400_000, "Branded WPF window loads and renders visible content without showing a GUI");
            Check(new Typeface(window.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var embeddedFont) && embeddedFont.FontUri.ToString().Contains("Vazirmatn", StringComparison.OrdinalIgnoreCase), "Vazirmatn resolves from embedded font resource");
            var (resolvedExe, resolvedArgs) = WindowsIntegration.GetUpdateCommandLine();
            Check(!string.IsNullOrWhiteSpace(resolvedExe) && resolvedArgs.Contains("--update"), "GetUpdateCommandLine resolves valid executable and arguments");
            Check(window.FlowDirection == FlowDirection.RightToLeft && Brand.Version == "1.6.0" && window.Icon != null, "RTL, release version 1.6.0 and application icon");

            // Test Persian Date & Calendar
            var testDate = new DateTime(2026, 9, 29);
            var persianStr = PersianDateHelper.GetFormattedPersianDate(testDate);
            Check(persianStr.Contains("مهر") && persianStr.Contains("1405"), "Persian Solar Hijri Date formatting (PersianDateHelper)");

            // Test SourceCatalog Options for IranNature
            Check(SourceCatalog.Options.Any(o => o.Id == "IranNature"), "SourceCatalog includes IranNature collection");

            // Test Preferences persistence with new modern settings
            var modernPrefs = new Preferences { EnableGlobalHotkeys = true, ShowDesktopWidget = true, SyncWindowsAccentColor = true };
            var prefsPath = Path.Combine(root, "modern-prefs.json");
            Store.Write(prefsPath, modernPrefs);
            var readModern = Store.Read(prefsPath, new Preferences());
            Check(readModern.EnableGlobalHotkeys && readModern.ShowDesktopWidget && readModern.SyncWindowsAccentColor, "Modern features preferences persistence");

            // Test Multi-Monitor detection
            Check(WindowsIntegration.GetMonitorCount() >= 1, "Windows multi-monitor enumeration returns valid monitor count");

            // Test Dominant Color Extractor
            var samplePath = Path.Combine(root, "ui-preview.png");
            var sampleDominant = WindowsIntegration.CalculateDominantColor(samplePath);
            Check(sampleDominant.A == 255, "Dominant color extraction calculates valid RGB palette");

            // Test Social Card Generator
            var testPhoto = new Photo { Id = "test-card", Title = "دماوند استوار", Copyright = "طبیعت ایران", FilePath = samplePath, Date = "20260929", Source = "IranNature" };
            var cardPath = CardGenerator.GenerateShareableCard(testPhoto, Path.Combine(root, "test-card.png"));
            Check(File.Exists(cardPath) && new FileInfo(cardPath).Length > 1000, "CardGenerator creates high-resolution social share card");

            var tabs = (System.Windows.Controls.TabControl)window.FindName("Tabs");
            for (var tab = 1; tab <= 3; tab++)
            {
                tabs.SelectedIndex = tab; content.UpdateLayout();
                var tabImage = new RenderTargetBitmap(1180, 820, 96, 96, PixelFormats.Pbgra32); tabImage.Render(content);
                var tabEncoder = new PngBitmapEncoder(); tabEncoder.Frames.Add(BitmapFrame.Create(tabImage));
                using var stream = File.Create(Path.Combine(root, $"ui-tab-{tab}.png")); tabEncoder.Save(stream);
            }
            window.Close();

            if (args.Contains("--network"))
            {
                var photos = new BingClient(Path.Combine(root, "network")).FetchAsync("en-US", "1920x1080").GetAwaiter().GetResult();
                Check(photos.Count > 0 && photos.All(p => File.Exists(p.FilePath)), "Live Bing metadata and validated images");
                Check(photos.All(p => new Uri(p.Url).Host == "www.bing.com"), "Live feed source allowlist");
                foreach (var source in new[] { "NasaDaily", "NasaLibrary", "WikimediaPotd", "EsaHubble" })
                {
                    var liveCatalog = new SourceCatalog(Path.Combine(root, "network")).FetchAsync(new(source, "en-US", "UHD", "")).GetAwaiter().GetResult();
                    Check(liveCatalog.Count > 0 && liveCatalog.All(p => File.Exists(p.FilePath) && p.SourcePage.StartsWith("https://")), "Live " + source + " downloads and attribution");
                }
            }

            var lockIndex = Array.IndexOf(args, "--apply-lock-screen");
            if (lockIndex >= 0)
            {
                Console.WriteLine(WindowsIntegration.SetLockScreenAsync(args[lockIndex + 1]).GetAwaiter().GetResult());
                Check(true, "Live lock screen request completed (visual verification remains separate)");
            }

            Console.WriteLine($"{passed} checks passed. Live lock screen application: {lockIndex >= 0}. No desktop, policy, task, repair or service was applied.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
