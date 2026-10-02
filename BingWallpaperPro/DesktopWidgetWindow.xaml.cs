using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace BingWallpaperPro;

public partial class DesktopWidgetWindow : Window
{
    readonly DispatcherTimer clockTimer;
    readonly DispatcherTimer hardwareTimer;
    readonly DispatcherTimer weatherTimer;
    Preferences preferences;
    Photo? currentPhoto;
    WeatherInfo? currentWeather;

    [DllImport("user32.dll")]
    static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    public DesktopWidgetWindow()
    {
        InitializeComponent();
        preferences = Store.Settings;

        // Position restoring
        if (preferences.WidgetLeft >= 0 && preferences.WidgetTop >= 0 &&
            preferences.WidgetLeft < SystemParameters.VirtualScreenWidth &&
            preferences.WidgetTop < SystemParameters.VirtualScreenHeight)
        {
            Left = preferences.WidgetLeft;
            Top = preferences.WidgetTop;
        }
        else
        {
            Left = Math.Max(20, SystemParameters.WorkArea.Right - Width - 30);
            Top = Math.Max(20, SystemParameters.WorkArea.Top + 40);
        }

        ApplyPreferences();
        UpdateClock();
        UpdateWallpaperInfo();
        UpdateHardware();
        _ = UpdateWeatherAsync();

        clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clockTimer.Tick += (_, _) => UpdateClock();
        clockTimer.Start();

        hardwareTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        hardwareTimer.Tick += (_, _) => UpdateHardware();
        hardwareTimer.Start();

        weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        weatherTimer.Tick += async (_, _) => await UpdateWeatherAsync();
        weatherTimer.Start();
    }

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        EnableAcrylicBlur();
        ApplyPinMode();
    }

    void Window_Activated(object sender, EventArgs e)
    {
        if (preferences.WidgetPinMode?.Equals("Desktop", StringComparison.OrdinalIgnoreCase) == true)
        {
            DesktopPinning.SendToBottom(this);
        }
    }

    void Window_Deactivated(object sender, EventArgs e)
    {
        if (preferences.WidgetPinMode?.Equals("Desktop", StringComparison.OrdinalIgnoreCase) == true)
        {
            DesktopPinning.SendToBottom(this);
        }
    }

    void EnableAcrylicBlur()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var accent = new AccentPolicy
            {
                AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                AccentFlags = 2,
                GradientColor = unchecked((int)0x66081420) // Translucent dark glass ABGR
            };
            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = 19, // WCA_ACCENT_POLICY
                SizeOfData = accentStructSize,
                Data = accentPtr
            };
            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }
        catch { }
    }

    public void ApplyPreferences()
    {
        preferences = Store.Settings;

        // 1. Opacity
        var op = Math.Clamp(preferences.WidgetOpacity, 0.2, 1.0);
        GlassRootBorder.Opacity = op;

        // 2. Visibility toggles
        ClockPanel.Visibility = preferences.WidgetShowClock ? Visibility.Visible : Visibility.Collapsed;
        WeatherChip.Visibility = preferences.WidgetShowWeather ? Visibility.Visible : Visibility.Collapsed;
        HardwareChip.Visibility = preferences.WidgetShowHardware ? Visibility.Visible : Visibility.Collapsed;
        WallpaperPanel.Visibility = preferences.WidgetShowWallpaperInfo ? Visibility.Visible : Visibility.Collapsed;
        QuickActionsPanel.Visibility = preferences.WidgetShowQuickActions ? Visibility.Visible : Visibility.Collapsed;

        // 3. Context Menu checks
        MenuToggleClock.IsChecked = preferences.WidgetShowClock;
        MenuToggleWeather.IsChecked = preferences.WidgetShowWeather;
        MenuToggleHardware.IsChecked = preferences.WidgetShowHardware;
        MenuToggleWallpaper.IsChecked = preferences.WidgetShowWallpaperInfo;
        MenuToggleActions.IsChecked = preferences.WidgetShowQuickActions;

        ApplyPinMode();
        _ = UpdateWeatherAsync();
    }

    void ApplyPinMode()
    {
        var mode = preferences.WidgetPinMode ?? "Desktop";
        DesktopPinning.ApplyPinMode(this, mode);

        PinModeBtn.Content = mode switch
        {
            "TopMost" => "📌",
            "Normal" => "🔲",
            _ => "🪟"
        };
        PinModeBtn.ToolTip = mode switch
        {
            "TopMost" => "حالت: همیشه روی همه پنجره‌ها (TopMost)",
            "Normal" => "حالت: پنجره عادی",
            _ => "حالت: چسبیده به پس‌زمینه دسکتاپ (والپیپر)"
        };
    }

    void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm:ss");
        PersianDateText.Text = PersianDateHelper.GetFormattedPersianDate(now);
        GregorianDateText.Text = now.ToString("dd MMM yyyy");
    }

    void UpdateHardware()
    {
        if (!preferences.WidgetShowHardware) return;
        try
        {
            var hw = HardwareMonitor.GetCurrentStatus();
            CpuText.Text = hw.CpuSummary;
            RamAndBatteryText.Text = hw.HasBattery 
                ? $"{hw.RamSummary} | {hw.BatterySummary}" 
                : hw.RamSummary;
        }
        catch { }
    }

    public async Task UpdateWeatherAsync(bool force = false)
    {
        if (!preferences.WidgetShowWeather) return;
        try
        {
            var city = string.IsNullOrWhiteSpace(preferences.WidgetCity) ? "تهران" : preferences.WidgetCity;
            currentWeather = await WeatherService.GetWeatherAsync(city, force);
            if (currentWeather != null)
            {
                WeatherIconText.Text = currentWeather.ConditionIcon;
                WeatherCityAndTemp.Text = $"{currentWeather.City} • {currentWeather.FormattedTemperature}";
                WeatherConditionText.Text = currentWeather.ConditionText;
                WeatherChip.ToolTip = currentWeather.DetailedTooltip;
            }
        }
        catch { }
    }

    public void UpdateWallpaperInfo()
    {
        try
        {
            var archive = Store.Read(Path.Combine(Store.Root, "archive.json"), new List<Photo>());
            currentPhoto = archive.FirstOrDefault(p => File.Exists(p.FilePath));
            if (currentPhoto != null)
            {
                WallpaperTitleText.Text = string.IsNullOrWhiteSpace(currentPhoto.Title) ? "منظره روز ویندوز" : currentPhoto.Title;
                WallpaperCreditText.Text = string.IsNullOrWhiteSpace(currentPhoto.Copyright) ? $"{currentPhoto.Source} • {currentPhoto.Market}" : currentPhoto.Copyright;
                if (preferences.Favorites.Contains(currentPhoto.Id))
                {
                    FavoriteBtn.Content = "ستاره‌دار ⭐";
                }
                else
                {
                    FavoriteBtn.Content = "علاقه‌مندی ❤️";
                }
            }
        }
        catch { }
    }

    void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            SnapToScreenEdges();
        }
    }

    void SnapToScreenEdges()
    {
        const double snapThreshold = 24.0;
        var workArea = SystemParameters.WorkArea;

        if (Math.Abs(Left - workArea.Left) < snapThreshold) Left = workArea.Left + 10;
        if (Math.Abs((Left + Width) - workArea.Right) < snapThreshold) Left = workArea.Right - Width - 10;
        if (Math.Abs(Top - workArea.Top) < snapThreshold) Top = workArea.Top + 10;
        if (Math.Abs((Top + Height) - workArea.Bottom) < snapThreshold) Top = workArea.Bottom - Height - 10;

        preferences.WidgetLeft = Left;
        preferences.WidgetTop = Top;
        Store.Save(preferences);
    }

    void Window_LocationChanged(object sender, EventArgs e)
    {
        if (IsLoaded && WindowState == WindowState.Normal)
        {
            try
            {
                preferences.WidgetLeft = Left;
                preferences.WidgetTop = Top;
                Store.Save(preferences);
            }
            catch { }
        }
    }

    void PinModeBtn_Click(object sender, RoutedEventArgs e)
    {
        var currentMode = preferences.WidgetPinMode ?? "Desktop";
        preferences.WidgetPinMode = currentMode switch
        {
            "Desktop" => "Normal",
            "Normal" => "TopMost",
            _ => "Desktop"
        };
        Store.Save(preferences);
        ApplyPinMode();
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        preferences.ShowDesktopWidget = false;
        Store.Save(preferences);
        Hide();
    }

    async void NextWallpaper_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            WallpaperTitleText.Text = "در حال دریافت تصویر بعدی…";
            await new WallpaperEngine().UpdateAsync();
            UpdateWallpaperInfo();
        }
        catch (Exception ex)
        {
            WallpaperTitleText.Text = "خطا در تغییر تصویر: " + ex.Message;
        }
    }

    void Favorite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentPhoto == null) return;
            preferences = Store.Settings;
            if (preferences.Favorites.Contains(currentPhoto.Id))
            {
                preferences.Favorites.Remove(currentPhoto.Id);
                FavoriteBtn.Content = "علاقه‌مندی ❤️";
            }
            else
            {
                preferences.Favorites.Add(currentPhoto.Id);
                FavoriteBtn.Content = "ستاره‌دار ⭐";
            }
            Store.Save(preferences);
        }
        catch { }
    }

    void SyncAccent_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentPhoto != null && File.Exists(currentPhoto.FilePath))
            {
                WindowsIntegration.SyncWindowsAccentColor(currentPhoto.FilePath);
            }
        }
        catch { }
    }

    void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        App.ShowMainWindow();
    }

    async void WeatherChip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        WeatherConditionText.Text = "در حال به‌روزرسانی…";
        await UpdateWeatherAsync(true);
    }

    // Context Menu Handlers
    void MenuPinDesktop_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetPinMode = "Desktop";
        Store.Save(preferences);
        ApplyPinMode();
    }

    void MenuPinNormal_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetPinMode = "Normal";
        Store.Save(preferences);
        ApplyPinMode();
    }

    void MenuPinTopMost_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetPinMode = "TopMost";
        Store.Save(preferences);
        ApplyPinMode();
    }

    void MenuToggleWeather_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetShowWeather = !preferences.WidgetShowWeather;
        Store.Save(preferences);
        ApplyPreferences();
    }

    void MenuToggleHardware_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetShowHardware = !preferences.WidgetShowHardware;
        Store.Save(preferences);
        ApplyPreferences();
    }

    void MenuToggleClock_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetShowClock = !preferences.WidgetShowClock;
        Store.Save(preferences);
        ApplyPreferences();
    }

    void MenuToggleWallpaper_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetShowWallpaperInfo = !preferences.WidgetShowWallpaperInfo;
        Store.Save(preferences);
        ApplyPreferences();
    }

    void MenuToggleActions_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetShowQuickActions = !preferences.WidgetShowQuickActions;
        Store.Save(preferences);
        ApplyPreferences();
    }

    void MenuOpacity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && double.TryParse(item.Tag?.ToString(), out var op))
        {
            preferences.WidgetOpacity = op;
            Store.Save(preferences);
            ApplyPreferences();
        }
    }
}
