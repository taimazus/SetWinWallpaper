using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace BingWallpaperPro;

public partial class MainWindow : Window
{
    Preferences preferences = new();
    List<Photo> photos = [];
    Photo Selected => Gallery.SelectedItem as Photo ?? throw new InvalidOperationException("ابتدا یک تصویر انتخاب کنید.");
    bool busy;
    DiagnosticReport? diagnosticReport;
    DesktopWidgetWindow? desktopWidget;
    System.Windows.Interop.HwndSource? hwndSource;

    public MainWindow()
    {
        InitializeComponent();
        Title = Brand.Title;
        MarketBox.ItemsSource = BingClient.Markets; LockMarketBox.ItemsSource = BingClient.Markets;
        ResolutionBox.ItemsSource = BingClient.Resolutions;
        FitBox.ItemsSource = new[] { "Fill", "Fit", "Stretch", "Center", "Span" };
        try { preferences = Store.Settings; }
        catch (Exception ex) { StatusText.Text = "خطا در خواندن تنظیمات؛ پیش‌فرض‌ها نمایش داده می‌شوند: " + ex.Message; }
        DesktopSourceBox.ItemsSource = SourceCatalog.Options; LockSourceBox.ItemsSource = SourceCatalog.Options;
        DesktopSourceBox.SelectedValue = preferences.DesktopSource; LockSourceBox.SelectedValue = preferences.LockSource;
        DesktopFolderBox.Text = string.IsNullOrWhiteSpace(preferences.DesktopFolder) ? preferences.NetworkSharePath : preferences.DesktopFolder;
        LockFolderBox.Text = string.IsNullOrWhiteSpace(preferences.LockFolder) ? preferences.LockNetworkSharePath : preferences.LockFolder;
        MarketBox.SelectedItem = preferences.Market; LockMarketBox.SelectedItem = preferences.LockMarket;
        ResolutionBox.SelectedItem = preferences.Resolution; FitBox.SelectedItem = preferences.Fit;
        ModeBox.SelectedIndex = preferences.Mode switch { "Previous" => 1, "Regions" => 2, "Random" => 3, _ => 0 };
        DesktopCheck.IsChecked = preferences.Desktop; LockCheck.IsChecked = preferences.LockScreen;
        TimeBox.Text = preferences.DailyTime;
        QuickSourceBox.ItemsSource = SourceCatalog.Options; QuickSourceBox.SelectedIndex = 0;
        ServerModeCheck.IsChecked = preferences.IsServerMode || WindowsIntegration.IsWindowsServer();
        ServerShareBox.Text = WindowsIntegration.GetDefaultServerSharePath(preferences.ServerShareName);
        HotkeysCheck.IsChecked = preferences.EnableGlobalHotkeys;
        DesktopWidgetCheck.IsChecked = preferences.ShowDesktopWidget;
        AccentColorCheck.IsChecked = preferences.SyncWindowsAccentColor;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        if (preferences.ShowDesktopWidget)
        {
            try { desktopWidget = new DesktopWidgetWindow(); desktopWidget.Show(); } catch { }
        }
        UpdateSourceControls();
        try { LoadArchive(); } catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    async Task Work(Func<Task> action, string success)
    {
        if (busy) return;
        busy = true; Tabs.IsEnabled = false; BusyBar.Visibility = Visibility.Visible; StatusText.Text = "در حال انجام…";
        try { await action(); StatusText.Text = success; }
        catch (Exception ex)
        {
            Store.Log(ex.ToString());
            var summary = ex.Message.ReplaceLineEndings(" • ");
            StatusText.Text = "عملیات کامل نشد: " + (summary.Length > 240 ? summary[..240] + "… جزئیات در گزارش اجراها." : summary);
            AppDialog.Show(this, ex.Message, "گزارش عملیات");
        }
        finally { busy = false; Tabs.IsEnabled = true; BusyBar.Visibility = Visibility.Collapsed; }
    }

    void SavePreferences()
    {
        if (!TimeOnly.TryParseExact(TimeBox.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("ساعت را با ارقام انگلیسی به شکل 09:00 وارد کنید.");
        preferences.DesktopSource = DesktopSourceBox.SelectedValue as string ?? throw new InvalidOperationException("منبع دسکتاپ را انتخاب کنید.");
        preferences.LockSource = LockSourceBox.SelectedValue as string ?? throw new InvalidOperationException("منبع لاک‌اسکرین را انتخاب کنید.");
        preferences.DesktopFolder = DesktopFolderBox.Text.Trim(); preferences.LockFolder = LockFolderBox.Text.Trim();
        preferences.NetworkSharePath = DesktopFolderBox.Text.Trim(); preferences.LockNetworkSharePath = LockFolderBox.Text.Trim();
        preferences.Market = MarketBox.SelectedItem as string ?? throw new InvalidOperationException("منطقه را انتخاب کنید.");
        preferences.LockMarket = LockMarketBox.SelectedItem as string ?? throw new InvalidOperationException("منطقه دوم را انتخاب کنید.");
        preferences.Resolution = ResolutionBox.SelectedItem as string ?? "UHD";
        preferences.Fit = FitBox.SelectedItem as string ?? "Fill";
        preferences.Mode = (ModeBox.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "Same";
        preferences.Desktop = DesktopCheck.IsChecked == true; preferences.LockScreen = LockCheck.IsChecked == true;
        preferences.DailyTime = TimeBox.Text;
        preferences.IsServerMode = ServerModeCheck.IsChecked == true;
        preferences.EnableGlobalHotkeys = HotkeysCheck.IsChecked == true;
        preferences.ShowDesktopWidget = DesktopWidgetCheck.IsChecked == true;
        preferences.SyncWindowsAccentColor = AccentColorCheck.IsChecked == true;
        Store.Save(preferences);
        RegisterHotkeys();
    }

    void LoadArchive(string? root = null)
    {
        photos = Store.Read(Path.Combine(root ?? Store.Root, "archive.json"), new List<Photo>()).Where(p => File.Exists(p.FilePath)).OrderByDescending(p => p.Date).ToList();
        Filter();
    }

    bool onlyFavorites;
    void Filter()
    {
        Gallery.ItemsSource = photos.Where(p => !onlyFavorites || preferences.Favorites.Contains(p.Id)).ToList();
        if (Gallery.Items.Count > 0) Gallery.SelectedIndex = 0;
    }

    void Gallery_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Gallery.SelectedItem is not Photo photo) { Preview.Source = null; PhotoTitle.Text = ""; PhotoCredit.Text = ""; EmptyPreview.Visibility = Visibility.Visible; return; }
        try { Preview.Source = Store.LoadImage(photo.FilePath); EmptyPreview.Visibility = Visibility.Collapsed; PhotoTitle.Text = photo.Title; PhotoCredit.Text = photo.Copyright; }
        catch (Exception ex) { Preview.Source = null; StatusText.Text = ex.Message; }
    }

    async Task FetchSource(bool lockScreen)
    {
        SavePreferences();
        photos = await new SourceCatalog().FetchAsync(PhotoSelection.Request(preferences, lockScreen));
        onlyFavorites = false;
        Filter();
        if (photos.Count == 0) throw new InvalidOperationException("منبع انتخاب‌شده تصویری ندارد.");
    }

    async void FetchSource_Click(object sender, RoutedEventArgs e) => await Work(async () =>
    {
        SavePreferences();
        var selectedSource = QuickSourceBox.SelectedValue as string ?? "Bing";
        var req = new SourceRequest(selectedSource, preferences.Market, preferences.Resolution, preferences.DesktopFolder, preferences.NetworkSharePath);
        photos = await new SourceCatalog().FetchAsync(req);
        onlyFavorites = false;
        Filter();
        if (photos.Count == 0) throw new InvalidOperationException("منبع انتخاب‌شده تصویری ندارد.");
    }, "تصاویر منبع با موفقیت دریافت و آماده شدند.");

    void ShowArchive_Click(object sender, RoutedEventArgs e) { onlyFavorites = false; LoadArchive(); }
    void ShowFavorites_Click(object sender, RoutedEventArgs e) { onlyFavorites = !onlyFavorites; Filter(); }
    void OpenImagesFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(Store.Root, "Images");
        Directory.CreateDirectory(dir);
        WindowsIntegration.Open(dir);
    }

    async void CreateServerShare_Click(object sender, RoutedEventArgs e) => await Work(async () =>
    {
        var shareName = string.IsNullOrWhiteSpace(preferences.ServerShareName) ? "Wallpapers" : preferences.ServerShareName;
        var unc = await WindowsIntegration.CreateOrUpdateSmbShareAsync(shareName, Store.Root);
        ServerShareBox.Text = unc;
        ServerShareStatusText.Text = $"وضعیت اشتراک: ✓ اشتراک شبکه ویندوز فعال است ({unc})";
        ServerShareStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x52, 0xD1, 0xB2));
        AppDialog.Show(this, $"پوشه مخزن تصاویر سرور با موفقیت در ویندوز به اشتراک گذاشته شد (SMB Share):\n\nنام اشتراک: {shareName}\nمسیر محلی: {Store.Root}\nآدرس شبکه برای کلاینت‌ها:\n{unc}\n\nمجوز خواندن (Read-Only) به کاربران شبکه اختصاص یافت.", "اشتراک‌گذاری در سرور");
    }, "اشتراک شبکه ویندوز با موفقیت ایجاد و فعال شد.");

    async void CheckServerShare_Click(object sender, RoutedEventArgs e) => await Work(async () =>
    {
        var shareName = string.IsNullOrWhiteSpace(preferences.ServerShareName) ? "Wallpapers" : preferences.ServerShareName;
        var active = await WindowsIntegration.IsSmbShareActiveAsync(shareName);
        if (active)
        {
            var unc = WindowsIntegration.GetDefaultServerSharePath(shareName);
            ServerShareStatusText.Text = $"وضعیت اشتراک: ✓ اشتراک فعال و در دسترس است ({unc})";
            ServerShareStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x52, 0xD1, 0xB2));
            AppDialog.Show(this, $"اشتراک شبکه ویندوز با نام '{shareName}' فعال است و کلاینت‌ها می‌توانند از مسیر زیر استفاده کنند:\n\n{unc}", "بررسی اشتراک سرور");
        }
        else
        {
            ServerShareStatusText.Text = "وضعیت اشتراک: ✕ اشتراک در ویندوز تعریف نشده است. روی دکمه «ایجاد و فعال‌سازی Share» کلیک کنید.";
            ServerShareStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE2, 0x61, 0x61));
            AppDialog.Show(this, $"اشتراک شبکه با نام '{shareName}' هنوز در ویندوز ایجاد نشده است.\nبرای فعال‌سازی خودکار، روی دکمه «ایجاد و فعال‌سازی Share در ویندوز» کلیک فرمایید.", "بررسی اشتراک سرور");
        }
    }, "وضعیت اشتراک شبکه بررسی شد.");

    void CopyServerShare_Click(object sender, RoutedEventArgs e)
    {
        var unc = ServerShareBox.Text.Trim();
        Clipboard.SetText(unc);
        AppDialog.Show(this, $"آدرس اشتراکی زیر در کلیپ‌بورد کپی شد:\n\n{unc}\n\nکلاینت‌ها می‌توانند در سیستم‌های خود، منبع دسکتاپ/لاک‌اسکرین را روی «مخزن اشتراکی سرور» گذاشته و این آدرس را وارد کنند.", "کپی آدرس مخزن سرور");
    }

    async void SyncAllSources_Click(object sender, RoutedEventArgs e) => await Work(async () =>
    {
        var catalog = new SourceCatalog();
        var synced = await catalog.SyncAllOnlineSourcesAsync(ResolutionBox.SelectedItem as string ?? "UHD");
        LoadArchive();
        AppDialog.Show(this, $"همگام‌سازی تمامی گالری‌ها با موفقیت انجام شد.\nتعداد {synced} تصویر جدید از منابع آنلاین دریافت و در مخزن سرور ذخیره شد.", "همگام‌سازی مخزن سرور");
    }, "همگام‌سازی تمامی گالری‌های سرور انجام شد.");

    void OpenServerFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = Store.Root;
        Directory.CreateDirectory(path);
        WindowsIntegration.Open(path);
    }

    void ServerShareGuide_Click(object sender, RoutedEventArgs e)
    {
        var machine = Environment.MachineName;
        var physicalPath = Store.Root;
        var guide = $"راهنمای راه‌اندازی مخزن مرکزی تصاویر در سرور:\n\n" +
                    $"۱. پوشه محلی داده‌های سرور:\n{physicalPath}\n\n" +
                    $"۲. برای اشتراک‌گذاری خودکار، روی دکمه «ایجاد و فعال‌سازی Share در ویندوز» کلیک کنید یا در PowerShell با دسترسی Administrator دستور زیر را اجرا فرمایید:\n" +
                    $"New-SmbShare -Name Wallpapers -Path \"{physicalPath}\" -ReadAccess \"Everyone\"\n\n" +
                    $"۳. آدرس قابل استفاده برای کاربران و کلاینت‌ها:\n\\\\{machine}\\Wallpapers\n\n" +
                    $"۴. در کلاینت‌ها، در بخش منابع گزینه «مخزن اشتراکی سرور (شبکه سازمانی / UNC)» را انتخاب و آدرس بالا را درج فرمایید.";
        AppDialog.Show(this, guide, "راهنمای اشتراک‌گذاری در سرور");
    }

    async void Fetch_Click(object sender, RoutedEventArgs e) => await Work(() => FetchSource(false), "تصاویر منبع دسکتاپ آماده شد.");
    async void FetchLock_Click(object sender, RoutedEventArgs e) => await Work(() => FetchSource(true), "تصاویر منبع مؤثر لاک‌اسکرین آماده شد.");
    void SourceSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSourceControls();

    void UpdateSourceControls()
    {
        if (LockFolderBox == null || ModeBox == null) return;
        var independent = (ModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Regions";
        LockSourceBox.IsEnabled = independent;
        MarketBox.IsEnabled = DesktopSourceBox.SelectedValue as string == "Bing";
        LockMarketBox.IsEnabled = independent && LockSourceBox.SelectedValue as string == "Bing";
        DesktopFolderBox.IsEnabled = DesktopSourceBox.SelectedValue as string is "Folder" or "SharedNetwork";
        LockFolderBox.IsEnabled = independent && LockSourceBox.SelectedValue as string is "Folder" or "SharedNetwork";
    }

    void PickFolder(System.Windows.Controls.TextBox target)
    {
        var dialog = new OpenFolderDialog { Title = "انتخاب پوشه محلی یا مخزن شبکه سرور" };
        if (dialog.ShowDialog(this) == true) target.Text = dialog.FolderName;
    }

    void DesktopFolder_Click(object sender, RoutedEventArgs e) => PickFolder(DesktopFolderBox);
    void LockFolder_Click(object sender, RoutedEventArgs e) => PickFolder(LockFolderBox);

    async void SourcePage_Click(object sender, RoutedEventArgs e) => await Work(() =>
    {
        var page = Selected.SourcePage;
        if (!Uri.TryCreate(page, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0 ||
            uri.Host is not ("www.bing.com" or "www.nasa.gov" or "images.nasa.gov" or "commons.wikimedia.org" or "esahubble.org" or "www.eso.org" or "unsplash.com" or "eros.usgs.gov"))
            throw new InvalidOperationException("صفحه اصلی قابل اعتماد برای این تصویر موجود نیست.");
        WindowsIntegration.Open(uri.AbsoluteUri); return Task.CompletedTask;
    }, "صفحه اصلی تصویر باز شد.");

    async void Archive_Click(object sender, RoutedEventArgs e) => await Work(() => { LoadArchive(); return Task.CompletedTask; }, "آرشیو محلی باز شد.");
    void Filter_Click(object sender, RoutedEventArgs e) => Filter();
    async void Import_Click(object sender, RoutedEventArgs e) => await Work(async () => { var imported = await Task.Run(() => WindowsIntegration.ImportSpotlight()); LoadArchive(); if (imported.Count == 0) throw new InvalidOperationException("تصویر مناسب در کش Spotlight یافت نشد."); }, "تصاویر موجود در کش Spotlight وارد شدند.");
    async void Desktop_Click(object sender, RoutedEventArgs e) => await Work(() => { WindowsIntegration.SetDesktop(Selected.FilePath, FitBox.SelectedItem as string ?? "Fill"); return Task.CompletedTask; }, "تصویر دسکتاپ اعمال شد.");
    async void Lock_Click(object sender, RoutedEventArgs e) => await Work(async () => { await WindowsIntegration.SetLockScreenAsync(Selected.FilePath); }, "ویندوز درخواست تغییر تصویر لاک‌اسکرین را پذیرفت.");

    async Task ManagedLockAction(string action)
    {
        var image = action == "Apply" ? Selected.FilePath : "";
        var script = Path.Combine(AppContext.BaseDirectory, "Scripts", "Set-ManagedLockScreen.ps1");
        var command = "& " + WindowsIntegration.QuotePS(script) + " -Action " + action + " -ImagePath " + WindowsIntegration.QuotePS(image) + "; exit $LASTEXITCODE";
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        info.Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
        using var process = Process.Start(info) ?? throw new IOException("Cannot launch policy helper.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException("اعمال سیاست کامل نشد؛ این قابلیت مخصوص Server/Enterprise/Education خارج از دامنه است. گزارش BingWallpaperPro-LockScreen-Setup.log در پوشه موقت Administrator را بررسی کنید.");
    }

    async void ManagedLock_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Show(this, "تصویر انتخاب‌شده به عنوان سیاست لاک‌اسکرین کل دستگاه ثبت می‌شود. این روش مخصوص Server/Enterprise/Education خارج از دامنه است؛ ممکن است ورود مجدد لازم باشد. ادامه؟", "لاک‌اسکرین مدیریتی", true)) return;
        await Work(() => ManagedLockAction("Apply"), "سیاست لاک‌اسکرین ثبت شد؛ نمایش تصویر را پس از ورود مجدد بررسی کنید.");
    }

    async void RestorePolicy_Click(object sender, RoutedEventArgs e) => await Work(() => ManagedLockAction("Restore"), "سیاست لاک‌اسکرین به وضعیت قبلی بازگردانده شد.");
    async void Favorite_Click(object sender, RoutedEventArgs e) => await Work(() => { var id = Selected.Id; if (!preferences.Favorites.Remove(id)) preferences.Favorites.Add(id); SavePreferences(); Filter(); return Task.CompletedTask; }, "علاقه‌مندی‌ها ذخیره شدند.");
    async void Export_Click(object sender, RoutedEventArgs e) => await Work(() => { var photo = Selected; var dialog = new SaveFileDialog { Filter = "Image (*.jpg)|*.jpg", FileName = photo.Date + "-" + photo.Id + ".jpg" }; if (dialog.ShowDialog(this) == true) File.Copy(photo.FilePath, dialog.FileName, true); return Task.CompletedTask; }, "عملیات ذخیره تصویر پایان یافت.");
    async void Save_Click(object sender, RoutedEventArgs e) => await Work(() => { SavePreferences(); return Task.CompletedTask; }, "تنظیمات ذخیره شد؛ برای تغییر ساعت، زمان‌بندی را نیز اصلاح کنید.");
    async void Update_Click(object sender, RoutedEventArgs e) => await Work(async () => { SavePreferences(); await new WallpaperEngine().UpdateAsync(); LoadArchive(); }, "به‌روزرسانی انتخاب‌های روزانه انجام شد.");
    async void Schedule_Click(object sender, RoutedEventArgs e) => await Work(async () => { SavePreferences(); await WindowsIntegration.InstallScheduleAsync(preferences.DailyTime); }, "زمان‌بندی روزانه و هنگام ورود فعال شد.");
    async void Unschedule_Click(object sender, RoutedEventArgs e) => await Work(async () => { await WindowsIntegration.RemoveScheduleAsync(); }, "زمان‌بندی حذف شد.");

    async void Diagnose_Click(object sender, RoutedEventArgs e) => await Work(async () =>
    {
        DiagnosisSummary.Text = "در حال بررسی؛ آزمون شبکه ممکن است تا ۴۵ ثانیه زمان ببرد…";
        diagnosticReport = await Diagnostics.RunAsync(NetworkDiagnosticsCheck.IsChecked == true);
        DiagnosticText.Text = diagnosticReport.ToText();
        DiagnosisSummary.Text = diagnosticReport.Summary;
    }, "عیب‌یابی کامل شد؛ وضعیت هر بخش و راهکار اصلاح در گزارش آمده است.");

    async void AutoRepair_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Show(this, "برنامه تنظیمات، کش فایل‌ها، فایل‌های تکراری و زمان‌بندی روزانه را بررسی و خودکار اصلاح خواهد کرد. ادامه می‌دهید؟", "رفع خودکار تمام ایرادات", true)) return;
        await Work(async () =>
        {
            var result = await Diagnostics.AutoRepairAllAsync();
            diagnosticReport = result.UpdatedReport;
            DiagnosticText.Text = result.Summary + "\n\n" + new string('─', 40) + "\n\n" + diagnosticReport.ToText();
            DiagnosisSummary.Text = diagnosticReport.Summary;
            preferences = Store.Settings;
            DesktopCheck.IsChecked = preferences.Desktop; LockCheck.IsChecked = preferences.LockScreen;
            TimeBox.Text = preferences.DailyTime;
            LoadArchive();
        }, "عملیات رفع خودکار ایرادات انجام شد؛ گزارش وضعیت به‌روزرسانی شد.");
    }

    async void RepairSettings_Click(object sender, RoutedEventArgs e) => await Work(() =>
    {
        preferences = Diagnostics.RepairSettings();
        DesktopCheck.IsChecked = preferences.Desktop; LockCheck.IsChecked = preferences.LockScreen;
        TimeBox.Text = preferences.DailyTime;
        return Task.CompletedTask;
    }, "تنظیمات بررسی، تعمیر و ذخیره شدند.");

    async void RepairArchive_Click(object sender, RoutedEventArgs e) => await Work(() =>
    {
        var count = Diagnostics.RepairArchive();
        LoadArchive();
        return Task.CompletedTask;
    }, "آرشیو بررسی شد و موارد آسیب‌دیده و تکراری پاک‌سازی شدند.");

    async void PurgeDuplicates_Click(object sender, RoutedEventArgs e) => await Work(() =>
    {
        var purged = SourceCatalog.PurgeDuplicates();
        LoadArchive();
        return Task.CompletedTask;
    }, "فایل‌های تکراری بر اساس محتوا بررسی و حذف شدند.");

    async void RepairSchedule_Click(object sender, RoutedEventArgs e) => await Work(async () =>
    {
        SavePreferences();
        await WindowsIntegration.InstallScheduleAsync(preferences.DailyTime);
    }, "زمان‌بندی روزانه با مسیر فعلی برنامه مجدداً تعمیر و ثبت شد.");

    async void Settings_Click(object sender, RoutedEventArgs e) => await Work(() => { WindowsIntegration.Open("ms-settings:lockscreen"); return Task.CompletedTask; }, "تنظیمات ویندوز باز شد.");
    async void Log_Click(object sender, RoutedEventArgs e) => await Work(async () => { diagnosticReport = null; var path = Path.Combine(Store.Root, "activity.log"); DiagnosticText.Text = File.Exists(path) ? await File.ReadAllTextAsync(path) : "هنوز گزارشی ثبت نشده است."; }, "گزارش اجراها نمایش داده شد.");
    async void Report_Click(object sender, RoutedEventArgs e) => await Work(() => { var dialog = new SaveFileDialog { Filter = diagnosticReport == null ? "Text (*.txt)|*.txt" : "Text (*.txt)|*.txt|JSON (*.json)|*.json", FileName = "SahandNama-diagnostics", AddExtension = true }; if (dialog.ShowDialog(this) == true) { if (dialog.FilterIndex == 2 && diagnosticReport != null) Store.Write(dialog.FileName, diagnosticReport); else File.WriteAllText(dialog.FileName, DiagnosticText.Text); } return Task.CompletedTask; }, "عملیات خروجی گزارش پایان یافت.");

    async void Repair_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Show(this, "ابتدا حالت لاک‌اسکرین را در Settings ویندوز روی Picture بگذارید و پنجره Settings را ببندید. کلیه تنظیمات، کش ناقص، کلیدهای رجیستری و پکیج‌های Spotlight تعمیر و بازنشانی می‌شوند. ادامه می‌دهید؟", "بازنشانی کامل Spotlight", true)) return;
        await Work(async () => { diagnosticReport = null; DiagnosticText.Text = await WindowsIntegration.ResetSpotlightAsync(); }, "فرآیند کامل تعمیر و بازنشانی Spotlight انجام شد.");
    }

    async void Restore_Click(object sender, RoutedEventArgs e) => await Work(() => { WindowsIntegration.RestoreDesktop(); return Task.CompletedTask; }, "پس‌زمینه اولیه بازیابی شد.");
    async void Backups_Click(object sender, RoutedEventArgs e) => await Work(() => { var path = Path.Combine(Store.Root, "Backups"); Directory.CreateDirectory(path); WindowsIntegration.Open(path); return Task.CompletedTask; }, "پوشه پشتیبان‌ها باز شد.");

    async void InstallService_Click(object sender, RoutedEventArgs e) => await Work(async () =>
    {
        var msg = await WindowsIntegration.InstallServiceAsync();
        AppDialog.Show(this, msg, "سرویس ویندوز");
    }, "سرویس دانلود نصب و راه‌اندازی شد.");

    async void RemoveService_Click(object sender, RoutedEventArgs e) => await Work(async () =>
    {
        var msg = await WindowsIntegration.RemoveServiceAsync();
        AppDialog.Show(this, msg, "سرویس ویندوز");
    }, "سرویس حذف شد؛ تصاویر آرشیو حفظ شده‌اند.");
    async void ServiceArchive_Click(object sender, RoutedEventArgs e) => await Work(() => { LoadArchive(Store.SharedRoot); if (photos.Count == 0) throw new InvalidOperationException("آرشیو سرویس هنوز تصویری ندارد."); Tabs.SelectedIndex = 0; return Task.CompletedTask; }, "آرشیو سرویس باز شد.");
    async void Company_Click(object sender, RoutedEventArgs e) => await Work(() => { WindowsIntegration.Open(Brand.Website); return Task.CompletedTask; }, "وب‌سایت شرکت باز شد.");
    void Company_Navigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) { Company_Click(sender, e); e.Handled = true; }
    async void Help_Click(object sender, RoutedEventArgs e) => await Work(() =>
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Guide.fa.html"),
            Path.Combine(Environment.CurrentDirectory, "Guide.fa.html"),
            Path.Combine(AppContext.BaseDirectory, "..", "Guide.fa.html"),
            Path.Combine(AppContext.BaseDirectory, "README.md"),
            Path.Combine(Environment.CurrentDirectory, "README.md")
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path != null)
        {
            WindowsIntegration.Open(path);
            return Task.CompletedTask;
        }
        WindowsIntegration.Open(Brand.Website);
        return Task.CompletedTask;
    }, "راهنمای برنامه باز شد.");
    async void OpenData_Click(object sender, RoutedEventArgs e) => await Work(() => { Directory.CreateDirectory(Store.Root); WindowsIntegration.Open(Store.Root); return Task.CompletedTask; }, "پوشه داده‌های برنامه باز شد.");

    void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            hwndSource = System.Windows.Interop.HwndSource.FromHwnd(handle);
            hwndSource?.AddHook(HwndHook);
            RegisterHotkeys();
        }
        catch { }
    }

    void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        UnregisterHotkeys();
    }

    void RegisterHotkeys()
    {
        try
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            UnregisterHotkeys();
            if (preferences.EnableGlobalHotkeys)
            {
                // Win + Alt + W (Next Wallpaper)
                WindowsIntegration.RegisterHotKey(handle, WindowsIntegration.HOTKEY_ID_NEXT_WALLPAPER, WindowsIntegration.MOD_WIN | WindowsIntegration.MOD_ALT | WindowsIntegration.MOD_NOREPEAT, 0x57);
                // Win + Alt + S (Favorite Wallpaper)
                WindowsIntegration.RegisterHotKey(handle, WindowsIntegration.HOTKEY_ID_FAVORITE, WindowsIntegration.MOD_WIN | WindowsIntegration.MOD_ALT | WindowsIntegration.MOD_NOREPEAT, 0x53);
            }
        }
        catch { }
    }

    void UnregisterHotkeys()
    {
        try
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                WindowsIntegration.UnregisterHotKey(handle, WindowsIntegration.HOTKEY_ID_NEXT_WALLPAPER);
                WindowsIntegration.UnregisterHotKey(handle, WindowsIntegration.HOTKEY_ID_FAVORITE);
            }
        }
        catch { }
    }

    IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowsIntegration.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == WindowsIntegration.HOTKEY_ID_NEXT_WALLPAPER)
            {
                Update_Click(this, new RoutedEventArgs());
                handled = true;
            }
            else if (id == WindowsIntegration.HOTKEY_ID_FAVORITE)
            {
                Favorite_Click(this, new RoutedEventArgs());
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    async void MultiMonitor_Click(object sender, RoutedEventArgs e) => await Work(() =>
    {
        var count = WindowsIntegration.GetMonitorCount();
        for (uint i = 0; i < count; i++)
        {
            WindowsIntegration.SetMonitorWallpaper(i, Selected.FilePath);
        }
        return Task.CompletedTask;
    }, "تصویر روی تمامی مانیتورهای سیستم اعمال شد.");

    async void ShareCard_Click(object sender, RoutedEventArgs e) => await Work(() =>
    {
        var path = CardGenerator.GenerateShareableCard(Selected);
        WindowsIntegration.Open(Path.GetDirectoryName(path)!);
        return Task.CompletedTask;
    }, "کارت گرافیکی منظره ایجاد شد و پوشه مربوطه باز گردید.");

    void OpenDesktopWidget_Click(object sender, RoutedEventArgs e)
    {
        if (desktopWidget == null || !desktopWidget.IsLoaded)
        {
            desktopWidget = new DesktopWidgetWindow();
            desktopWidget.Show();
            preferences.ShowDesktopWidget = true;
            DesktopWidgetCheck.IsChecked = true;
            Store.Save(preferences);
        }
        else
        {
            desktopWidget.Activate();
        }
    }

    void ToggleDesktopIcons_Click(object sender, RoutedEventArgs e)
    {
        WindowsIntegration.ToggleDesktopIcons();
        StatusText.Text = "فرمان پنهان/نمایان‌سازی آیکون‌های دسکتاپ ارسال شد.";
    }
}
