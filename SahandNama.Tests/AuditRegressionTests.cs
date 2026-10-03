using SahandNama;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class AuditRegressionTests
{
    sealed class UnpumpedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) { /* Simulate a blocked UI dispatcher. */ }
    }
    public static void Run(Action<bool, string> check)
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "audit-regression", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var outside = Path.Combine(root, "user-document.txt");
        File.WriteAllText(outside, "preserve this document");
        var store = Path.Combine(root, "store");
        Store.Write(Path.Combine(store, "archive.json"), new[] { new Photo { Id = "outside", FilePath = outside } });
        Diagnostics.RepairArchive(store);
        check(File.ReadAllText(outside) == "preserve this document" && Store.Read(Path.Combine(store, "archive.json"), new List<Photo>()).Count == 0, "D01 repair removes invalid metadata without deleting external files");
        var archive = Path.Combine(store, "archive.json");
        File.WriteAllText(archive, "{broken-metadata");
        Diagnostics.RepairArchive(store);
        check(Directory.GetFiles(store, "archive.json.corrupt.*.bak").Any(p => File.ReadAllText(p) == "{broken-metadata"), "D04 corrupt archive bytes are backed up before replacement");
        using (var held = Store.AcquireLockAsync(Path.Combine(store, "feed.lock")).GetAwaiter().GetResult())
        {
            var repair = Task.Run(() => Diagnostics.RepairArchive(store));
            check(!repair.Wait(350), "R02 repair waits for active feed writer");
            held.Dispose();
            check(repair.Wait(5000), "R02 repair completes after writer releases lock");
        }
        Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() => Store.MergeArchive(store, new[] { new Photo { Id = "parallel-" + i } })))).GetAwaiter().GetResult();
        check(Store.Read(archive, new List<Photo>()).Count == 20, "R02 concurrent archive merges preserve every writer");
        var heldLock = Store.AcquireLockAsync(Path.Combine(store, "ui.lock")).GetAwaiter().GetResult();
        using var contenderStarted = new ManualResetEventSlim();
        var uiContender = Task.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new UnpumpedContext());
            var wait = Store.AcquireLockAsync(Path.Combine(store, "ui.lock"), 2);
            contenderStarted.Set();
            using var acquired = wait.GetAwaiter().GetResult();
        });
        check(contenderStarted.Wait(2000), "N10 UI contention reproduction starts");
        heldLock.Dispose();
        check(uiContender.Wait(4000), "N10 synchronous UI lock caller completes without dispatching continuations");
        Store.Write(Path.Combine(store, "settings.json"), new Preferences());
        var stale = Store.Read(Path.Combine(store, "settings.json"), new Preferences());
        var baseline = Store.CloneSettings(stale);
        Store.UpdateSettings(p => p.Favorites.Add("favorite"), store);
        Store.UpdateSettings(p => p.WidgetLeft = 50, store);
        stale.Market = "ja-JP";
        Store.UpdateSettings(p => p.WidgetPinMode = "TopMost", store);
        Store.SaveEditorSettings(stale, baseline, store);
        var saved = Store.Read(Path.Combine(store, "settings.json"), new Preferences());
        check(saved.Favorites.SequenceEqual(new[] { "favorite" }) && saved.WidgetLeft == 50 && saved.Market == "ja-JP" && saved.WidgetPinMode == "TopMost", "D05 editor changes preserve newer favorites, coordinates and unchanged widget fields");
        File.WriteAllText(Path.Combine(store, "settings.json"), "{broken-settings");
        check(Store.ReadSettingsForStartup(store).Desktop && File.ReadAllText(Path.Combine(store, "settings.json")) == "{broken-settings", "D10 startup defaults preserve corrupt settings for explicit repair");
        Diagnostics.RepairSettings(store);
        check(Directory.GetFiles(store, "settings.json.corrupt.*.bak").Any(p => File.ReadAllText(p) == "{broken-settings"), "D04 corrupt settings preserve original bytes before replacement");
        var versioned = Path.Combine(root, "SahandNama-v1.8.0-win-x64.exe"); File.WriteAllText(versioned, "new version");
        File.WriteAllText(Path.Combine(root, "SahandNama.exe"), "old version");
        check(WindowsIntegration.GetUpdateCommandLine(root, versioned, true).Executable == versioned, "D02 running versioned app wins over stale neighboring canonical executable");
        check(Store.CurrentDesktopPhoto(store) == null, "D06 missing current metadata does not guess from archive");
        Store.RecordDesktopPhoto(new Photo { Id = "selected-b", FilePath = outside }, store);
        check(Store.CurrentDesktopPhoto(store)?.Id == "selected-b", "D06 current desktop identity round trips independently of archive order");
        var candidates = new List<Photo> { new() { Id = "a" }, new() { Id = "b" }, new() { Id = "c" } };
        var day = new DateOnly(2026, 10, 3);
        var fetches = 0;
        Task<List<Photo>> Fetch(SourceRequest _) { fetches++; return Task.FromResult(candidates); }
        var follow = new PhotoSelectionSession(new Preferences { DesktopMode = "Random", LockMode = "Follow" }, Fetch, day);
        var first = follow.SelectAsync(false).GetAwaiter().GetResult();
        check(Enumerable.Range(0, 100).All(_ => follow.SelectAsync(true).GetAwaiter().GetResult().Id == first.Id) && fetches == 1, "D03 random Follow reuses the exact desktop selection and one fetch");
        var independent = new Preferences { DesktopMode = "Daily", LockMode = "Random", Mode = "Random" };
        check(PhotoSelection.DesktopMode(independent) == "Daily", "D03 random lock mode does not override daily desktop");
        var previous = new PhotoSelectionSession(new Preferences { DesktopMode = "Random", LockMode = "Previous" }, Fetch, day);
        check(previous.SelectAsync(false).GetAwaiter().GetResult().Id != previous.SelectAsync(true).GetAwaiter().GetResult().Id, "D03 Previous follows the chosen desktop even when desktop is random");
        File.WriteAllText(archive, "null");
        Diagnostics.RepairArchive(store);
        check(Directory.GetFiles(store, "archive.json.corrupt.*.bak").Any(p => File.ReadAllText(p) == "null"), "N06 null archive is backed up and repaired");
        File.WriteAllText(archive, "[null]");
        check(Diagnostics.RepairArchive(store) == 1, "N06 null archive records are removed without crashing repair");
        File.WriteAllText(Path.Combine(store, "settings.json"), "{\"Favorites\":null}");
        check(Diagnostics.RepairSettings(store).Favorites.Count == 0, "N06 explicit null favorites are normalized by repair");
        var errorsRoot = Path.Combine(root, "repair-errors"); Directory.CreateDirectory(errorsRoot);
        File.WriteAllText(Path.Combine(errorsRoot, "Images"), "directory creation must fail");
        var partial = Diagnostics.AutoRepairAllAsync(errorsRoot).GetAwaiter().GetResult();
        check(partial.Errors.Count > 0 && partial.ErrorSummary.Length > 0 && !partial.RepairedItems.Any(s => s.StartsWith("خطا")), "N07 partial repair reports errors separately from successful operations");
        NetworkChecks(root, check);
    }

    sealed class Handler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request, token));
    }
    static void ExpectFailure(Action action, Action<bool, string> check, string name)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or InvalidOperationException or OperationCanceledException) { check(true, name); return; }
        throw new Exception("Expected failure: " + name);
    }
    static void NetworkChecks(string root, Action<bool, string> check)
    {
        var calls = 0;
        using var blockedRedirect = new HttpClient(new Handler((_, _) => { calls++; var r = new HttpResponseMessage(HttpStatusCode.Redirect); r.Headers.Location = new Uri("https://localhost/private"); return r; }));
        ExpectFailure(() => SourceHttp.ReadAsync("https://www.nasa.gov/feed", 100, default, blockedRedirect).GetAwaiter().GetResult(), check, "R01 redirect to forbidden host is rejected");
        check(calls == 1, "R01 no request reaches forbidden redirect target");
        calls = 0;
        using var allowedRedirect = new HttpClient(new Handler((_, _) => { calls++; var r = new HttpResponseMessage(calls == 1 ? HttpStatusCode.Redirect : HttpStatusCode.OK); if (calls == 1) r.Headers.Location = new Uri("https://images-assets.nasa.gov/photo"); else r.Content = new StringContent("ok"); return r; }));
        check(System.Text.Encoding.UTF8.GetString(SourceHttp.ReadAsync("https://www.nasa.gov/feed", 100, default, allowedRedirect).GetAwaiter().GetResult()) == "ok" && calls == 2, "R01 allowed cross-host redirect works");
        using var loop = new HttpClient(new Handler((_, _) => { var r = new HttpResponseMessage(HttpStatusCode.Redirect); r.Headers.Location = new Uri("/loop", UriKind.Relative); return r; }));
        ExpectFailure(() => SourceHttp.ReadAsync("https://www.nasa.gov/feed", 100, default, loop).GetAwaiter().GetResult(), check, "R01 redirect loops are bounded");
        var cache = Path.Combine(root, "offline"); Directory.CreateDirectory(cache);
        var image = Path.Combine(cache, "sample.png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8)));
        using (var stream = File.Create(image)) encoder.Save(stream);
        Store.Write(Path.Combine(cache, "archive.json"), new[] { new Photo { Id = "nasa", Source = "NasaDaily", FilePath = image }, new Photo { Id = "bing", Source = "Bing", Market = "en-US", FilePath = image } });
        using var timeout = new HttpClient(new Handler((_, _) => throw new TaskCanceledException("simulated transport timeout")));
        var catalog = new SourceCatalog(cache, timeout);
        check(catalog.FetchAsync(new("NasaDaily", "en-US", "UHD", "")).GetAwaiter().GetResult().Single().Id == "nasa", "D11 metadata timeout falls back to healthy matching-source archive");
        check(new BingClient(cache, timeout).FetchAsync("en-US", "UHD").GetAwaiter().GetResult().Single().Id == "bing", "D11 Bing metadata timeout uses matching-market cache");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        ExpectFailure(() => catalog.FetchAsync(new("NasaDaily", "en-US", "UHD", ""), cancelled.Token).GetAwaiter().GetResult(), check, "D11 explicit caller cancellation is propagated");
        ExpectFailure(() => catalog.FetchAsync(new("MuseumArt", "en-US", "UHD", "")).GetAwaiter().GetResult(), check, "N02 fallback never substitutes another source");
        var empty = Path.Combine(root, "empty-folder"); Directory.CreateDirectory(empty);
        ExpectFailure(() => catalog.FetchAsync(new("Folder", "en-US", "UHD", empty)).GetAwaiter().GetResult(), check, "N02 empty personal folder does not silently use online archive");
        using var unavailable = new HttpClient(new Handler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        ExpectFailure(() => new SourceCatalog(cache, unavailable).SyncAllOnlineSourcesAsync().GetAwaiter().GetResult(), check, "D09 all-source failure is reported despite a populated cache");
        check(SourceCatalog.OnlineSourceIds.Contains("MuseumArt") && SourceCatalog.OnlineSourceIds.Contains("NasaLibrary") && SourceCatalog.OnlineSourceIds.Contains("IranNature") && SourceCatalog.OnlineSourceIds.Contains("Wallhaven"), "D09 server sync includes every online catalog family");
        check(new[] { "pt-BR", "it-IT", "es-ES" }.All(BingClient.Markets.Contains), "D14 global markets pass the common region validator");
        var share = Path.Combine(root, "share"); Directory.CreateDirectory(share);
        Store.Write(Path.Combine(share, "archive.json"), new[] { new Photo { Id = "outside", FilePath = image } });
        ExpectFailure(() => new SourceCatalog(Path.Combine(root, "share-client")).FetchSharedNetworkAsync(share).GetAwaiter().GetResult(), check, "N03 share metadata cannot import an absolute file outside the share");
        var serviceScript = WindowsIntegration.ServiceManagementScript("Install", root);
        check(serviceScript.Contains("SahandNama.exe") && serviceScript.Contains("NT AUTHORITY\\LocalService") && !serviceScript.Contains("BingWallpaperPro.exe"), "D02 embedded service installer uses published executable and least-privileged account");
        var images = Path.Combine(cache, "Images"); Directory.CreateDirectory(images);
        var copyA = Path.Combine(images, "a.jpg"); var copyB = Path.Combine(images, "b.jpg"); File.Copy(image, copyA); File.Copy(image, copyB);
        Store.Write(Path.Combine(cache, "archive.json"), new[] { new Photo { Id = "a", FilePath = copyA }, new Photo { Id = "b", FilePath = copyB } });
        Store.RecordDesktopPhoto(new Photo { Id = "b", FilePath = copyB }, cache);
        check(SourceCatalog.PurgeDuplicates(cache) == 1 && File.Exists(copyB) && !File.Exists(copyA), "N08 deduplication preserves the active desktop file as canonical");
        var oldBytes = File.ReadAllBytes(copyB);
        ExpectFailure(() => SourceCatalog.CopySharedImageAsync(image, copyB, default, (_, temp) => { File.WriteAllText(temp, "partial download"); throw new IOException("simulated interrupted share"); }).GetAwaiter().GetResult(), check, "N04 interrupted shared copy reports failure");
        check(File.ReadAllBytes(copyB).SequenceEqual(oldBytes) && !Directory.GetFiles(images, "*.tmp").Any(), "N04 failed copy preserves old image and cleans temporary bytes");
        var invalidImage = Path.Combine(root, "invalid-image.png"); File.WriteAllText(invalidImage, "corrupt");
        try { SourceCatalog.ConvertToJpeg(invalidImage, copyB); throw new Exception("Expected invalid image rejection"); }
        catch (Exception ex) when (ex is NotSupportedException or System.IO.FileFormatException or ArgumentException or IOException) { }
        check(File.ReadAllBytes(copyB).SequenceEqual(oldBytes) && !Directory.GetFiles(images, "*.tmp").Any(), "N15 invalid Spotlight conversion preserves existing destination and leaves no partial image");
        var resetDir = Path.Combine(root, "reset-settings"); Directory.CreateDirectory(resetDir);
        var resetFile = Path.Combine(resetDir, "settings.dat"); File.WriteAllText(resetFile, "original");
        var brokenBackup = Path.Combine(root, "backup-is-file"); File.WriteAllText(brokenBackup, "file");
        check(WindowsIntegration.BackupAndRemoveSpotlightSettings(resetDir, brokenBackup).Count == 1 && File.ReadAllText(resetFile) == "original", "N16 failed Spotlight backup is reported and original settings survive");
        var validBackup = Path.Combine(root, "valid-backup"); Directory.CreateDirectory(validBackup);
        check(WindowsIntegration.BackupAndRemoveSpotlightSettings(resetDir, validBackup).Count == 0 && !File.Exists(resetFile) && File.ReadAllText(Path.Combine(validBackup, "settings.dat")) == "original", "N16 successful Spotlight reset preserves bytes before removing settings");
        check(DownloadService.NextSyncDelay(false) == TimeSpan.FromHours(1) && DownloadService.NextSyncDelay(true) == TimeSpan.FromHours(12), "Hourly retry: failed service sync waits one hour; success retains twelve-hour cadence");
        check(catalog.UsedOfflineFallback, "Hourly retry: metadata timeout with usable cache remains marked offline for scheduler retry");
        Store.MergeArchive(cache, new[] { new Photo { Id = "retry-bing", Source = "Bing", Market = "en-US", FilePath = image } });
        var offlineBing = new SourceCatalog(cache, timeout);
        offlineBing.FetchAsync(new("Bing", "en-US", "UHD", "")).GetAwaiter().GetResult();
        check(offlineBing.UsedOfflineFallback, "Hourly retry: Bing cache fallback propagates to source catalog");
        using var partialTransport = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/missing.jpg") return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = request.RequestUri.AbsolutePath == "/good.png"
                ? new ByteArrayContent(File.ReadAllBytes(image))
                : new StringContent("<rss><channel><item><enclosure url='https://www.nasa.gov/missing.jpg' type='image/jpeg'/></item><item><enclosure url='https://www.nasa.gov/good.png' type='image/png'/></item></channel></rss>") };
        }));
        var partialCatalog = new SourceCatalog(Path.Combine(root, "partial-refresh"), partialTransport);
        check(partialCatalog.FetchAsync(new("NasaDaily", "en-US", "UHD", ""), allowOffline: false).GetAwaiter().GetResult().Count == 1 && partialCatalog.UsedOfflineFallback, "Hourly retry: one failed image keeps partial online refresh marked for retry despite another successful image");
        var scheduleScript = WindowsIntegration.ScheduleScript("09:00");
        var scheduleProbe = scheduleScript[..scheduleScript.IndexOf("Register-ScheduledTask -TaskName", StringComparison.Ordinal)];
        scheduleProbe += "\nif ($settings.RestartCount -ne 24 -or $settings.RestartInterval -ne 'PT1H' -or $action.Arguments -notmatch '--update --scheduled' -or $principal.LogonType -ne 'Interactive' -or $settings.MultipleInstances -ne 'IgnoreNew') { throw 'Unexpected scheduler settings' }; 'schedule-ok'";
        check(WindowsIntegration.RunPowerShellAsync(scheduleProbe).GetAwaiter().GetResult() == "schedule-ok", "Hourly retry: actual task objects use hourly restart, scheduled mode, interactive user and no overlap without registering a task");
        var commonsFixture = System.Text.Encoding.UTF8.GetBytes("""{"query":{"pages":{"2":{"title":"File:Painting.jpg","index":2,"imageinfo":[{"mime":"image/jpeg","width":2000,"height":1200,"thumburl":"https://thumb.wikimedia.org/wikipedia/commons/test.jpg","url":"https://upload.wikimedia.org/wikipedia/commons/original.jpg","extmetadata":{"Artist":{"value":"<b>Painter</b>"},"LicenseShortName":{"value":"CC BY-SA 4.0"}}}]},"1":{"title":"File:Small.jpg","index":1,"imageinfo":[{"mime":"image/jpeg","width":200,"height":100,"url":"https://upload.wikimedia.org/small.jpg"}]}}}}""");
        var commons = SourceCatalog.ParseCommonsImages(commonsFixture, "MuseumArt");
        check(commons.Count == 1 && commons[0].Source == "MuseumArt" && commons[0].Url.StartsWith("https://thumb.wikimedia.org/") && commons[0].Copyright.Contains("Painter") && commons[0].Copyright.Contains("CC BY-SA 4.0"), "N19 Commons museum parser preserves actual image URL, artist and license, and filters tiny images");
        check(SourceCatalog.ParseCommonsImages(commonsFixture, "IranNature", "عنوان ایران").Single().Title == "عنوان ایران" && SourceCatalog.MuseumArtFeed.StartsWith("https://commons.wikimedia.org/") && SourceCatalog.CommonsImageFeed("Stars Valley Qeshm").Contains("Stars%20Valley%20Qeshm"), "N19 Iran and museum resolve encoded Commons metadata queries instead of fabricated paths");
        var sharedPixels = new byte[128 * 128 * 4]; new Random(17).NextBytes(sharedPixels);
        for (var i = 3; i < sharedPixels.Length; i += 4) sharedPixels[i] = 255;
        var sharedEncoder = new PngBitmapEncoder(); sharedEncoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(128, 128, 96, 96, PixelFormats.Bgra32, null, sharedPixels, 128 * 4)));
        using var sharedStream = new MemoryStream(); sharedEncoder.Save(sharedStream);
        using var commonsTransport = new HttpClient(new Handler((request, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = request.RequestUri!.Host == "commons.wikimedia.org" ? new ByteArrayContent(commonsFixture) : new ByteArrayContent(sharedStream.ToArray()) }));
        var commonsCatalog = new SourceCatalog(Path.Combine(root, "commons-contract"), commonsTransport);
        check(Uri.UnescapeDataString(SourceCatalog.FeaturedNatureFeed).Contains("incategory:\"Featured pictures of landscapes\"") && SourceCatalog.FeaturedNatureFeed.Contains("format=json"), "N22 featured nature uses supported Commons category JSON query instead of invalid featured RSS feed");
        var featuredPhotos = commonsCatalog.FetchAsync(new("NatGeoNature", "en-US", "UHD", ""), allowOffline: false).GetAwaiter().GetResult();
        check(featuredPhotos.Count == 1 && featuredPhotos.All(p => SourceCatalog.IsHealthy(p)) && featuredPhotos[0].Source == "NatGeoNature" && featuredPhotos[0].Copyright.Contains("Painter") && !commonsCatalog.UsedOfflineFallback, "N22 featured nature pipeline downloads and archives valid category images with actual attribution without offline fallback");
        var iranPhotos = commonsCatalog.FetchAsync(new("IranNature", "en-US", "UHD", ""), allowOffline: false).GetAwaiter().GetResult();
        var museumPhotos = commonsCatalog.FetchAsync(new("MuseumArt", "en-US", "UHD", ""), allowOffline: false).GetAwaiter().GetResult();
        check(iranPhotos.Count > 0 && museumPhotos.Count > 0 && iranPhotos.All(p => SourceCatalog.IsHealthy(p)) && museumPhotos.All(p => SourceCatalog.IsHealthy(p)) && !commonsCatalog.UsedOfflineFallback, "N19 actual Iran/museum fetch pipelines resolve metadata, download, decode and archive with mocked HTTP");
        check(Store.Read(Path.Combine(root, "commons-contract", "archive.json"), new List<Photo>()).Select(p => p.Source).Contains("IranNature") && Store.Read(Path.Combine(root, "commons-contract", "archive.json"), new List<Photo>()).Select(p => p.Source).Contains("MuseumArt"), "N21 shared image bytes preserve independent source archive records");
        try { new SourceCatalog(Path.Combine(root, "source-error"), unavailable).FetchAsync(new("NasaDaily", "en-US", "UHD", "")).GetAwaiter().GetResult(); throw new Exception("Expected source error"); }
        catch (InvalidOperationException ex) { check(ex.Message.Contains("503", StringComparison.Ordinal), "N20 source error preserves actual HTTP reason in user-visible failure"); }
        var lockScript = WindowsIntegration.LockScreenScript(image);
        var readOnlyLockProbe = lockScript[..lockScript.IndexOf("if ($currentInstance -and $trySetMethod)", StringComparison.Ordinal)] + "\n} } } } } catch { throw }; if (!$file -or $file.Path -ne $fullPath -or !$trySetMethod) { throw 'WinRT lookup failed' }; 'reflection-ok'";
        try { WindowsIntegration.RunPowerShellAsync(readOnlyLockProbe.Replace("[Type[]]@([string])", "@([string])", StringComparison.Ordinal)).GetAwaiter().GetResult(); throw new Exception("Expected original overload binding failure"); }
        catch (InvalidOperationException ex) { check(ex.Message.Contains("BindingFlags", StringComparison.Ordinal), "N17 original untyped array reproduces screenshot overload binding error"); }
        check(WindowsIntegration.RunPowerShellAsync(readOnlyLockProbe).GetAwaiter().GetResult() == "reflection-ok", "N17 actual lock script opens StorageFile and resolves personalization methods without changing wallpaper");
        var fallbackLookup = "Add-Type -AssemblyName System.Runtime.WindowsRuntime; $s=[Type]::GetType('Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime'); $l=[Type]::GetType('Windows.System.UserProfile.LockScreen, Windows.System.UserProfile, ContentType=WindowsRuntime'); if (!$l.GetMethod('SetImageFileAsync', [Type[]]@($s))) { throw 'Missing fallback' }; 'fallback-ok'";
        check(WindowsIntegration.RunPowerShellAsync(fallbackLookup).GetAwaiter().GetResult() == "fallback-ok", "N17 fallback WinRT method lookup binds exact StorageFile signature without mutation");
        var priorCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            check(DesktopWidgetWindow.TryParseOpacity("0.80", out var opacity) && opacity == 0.8, "N11 opacity menu tags parse identically under comma-decimal cultures");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = priorCulture; }
        var titleCard = CardGenerator.GenerateShareableCard(new Photo { Id = "title-visible", Title = "عنوان فارسی باید داخل کارت دیده شود", Copyright = "اعتبار تصویر" }, Path.Combine(root, "title-card.png"));
        var otherCard = CardGenerator.GenerateShareableCard(new Photo { Id = "title-other", Title = "متن متفاوت", Copyright = "اعتبار تصویر" }, Path.Combine(root, "other-card.png"));
        var titlePixels = new byte[1080 * 1350 * 4]; var otherPixels = new byte[titlePixels.Length];
        Store.LoadImage(titleCard, 0).CopyPixels(titlePixels, 1080 * 4, 0); Store.LoadImage(otherCard, 0).CopyPixels(otherPixels, 1080 * 4, 0);
        check(Enumerable.Range(1070 * 1080 * 4, 160 * 1080 * 4).Count(i => titlePixels[i] != otherPixels[i]) > 100, "N14 Persian card titles render visibly inside the bottom text area");
    }
}
