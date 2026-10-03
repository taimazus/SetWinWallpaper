using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace SahandNama;

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
        preferences = Store.ReadSettingsForStartup();

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
            var op = Math.Clamp(preferences.WidgetOpacity, 0.0, 1.0);
            if (op <= 0.05)
            {
                // Disable DWM acrylic blur for 0% opacity - completely crystal transparent background
                var disableAccent = new AccentPolicy { AccentState = 0 }; // ACCENT_DISABLED
                var structSize = Marshal.SizeOf(disableAccent);
                var ptr = Marshal.AllocHGlobal(structSize);
                Marshal.StructureToPtr(disableAccent, ptr, false);
                var disableData = new WindowCompositionAttributeData
                {
                    Attribute = 19,
                    SizeOfData = structSize,
                    Data = ptr
                };
                SetWindowCompositionAttribute(hwnd, ref disableData);
                Marshal.FreeHGlobal(ptr);
                return;
            }

            byte alpha = (byte)Math.Clamp((int)(op * 120), 10, 200); // Tint alpha
            int gradientColor = unchecked((int)((alpha << 24) | 0x00201408)); // ABGR
            var accent = new AccentPolicy
            {
                AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                AccentFlags = 2,
                GradientColor = gradientColor
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
        catch (Exception ex)
        {
            Store.Log("Widget EnableAcrylicBlur warning: " + ex.Message);
        }
    }

    public void ApplyPreferences()
    {
        preferences = Store.ReadSettingsForStartup();

        // 1. Background-only Opacity (Foreground texts, clock and buttons remain 100% crisp & solid)
        var op = Math.Clamp(preferences.WidgetOpacity, 0.0, 1.0);
        GlassRootBorder.Opacity = 1.0;

        if (op <= 0.05)
        {
            // 0% Opacity: Pure floating text/controls directly on desktop wallpaper with zero container or box outline
            GlassRootBorder.Background = Brushes.Transparent;
            GlassRootBorder.BorderBrush = Brushes.Transparent;
            GlassRootBorder.BorderThickness = new Thickness(0);
            GlassRootBorder.Effect = null;

            WeatherChip.Background = Brushes.Transparent;
            WeatherChip.BorderBrush = Brushes.Transparent;
            WeatherChip.BorderThickness = new Thickness(0);

            HardwareChip.Background = Brushes.Transparent;
            HardwareChip.BorderBrush = Brushes.Transparent;
            HardwareChip.BorderThickness = new Thickness(0);

            WallpaperPanel.Background = Brushes.Transparent;
            WallpaperPanel.BorderBrush = Brushes.Transparent;
            WallpaperPanel.BorderThickness = new Thickness(0);
        }
        else
        {
            GlassRootBorder.BorderThickness = new Thickness(1.2);
            GlassRootBorder.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 4, Opacity = Math.Clamp(op * 0.7, 0.2, 0.6) };

            byte bgAlpha = (byte)Math.Clamp((int)(op * 255), 10, 255);
            byte borderAlpha = (byte)Math.Clamp((int)(op * 180 + 35), 20, 240);
            byte chipBgAlpha = (byte)Math.Clamp((int)(op * 38), 6, 75);
            byte chipBorderAlpha = (byte)Math.Clamp((int)(op * 51), 10, 100);

            GlassRootBorder.Background = new SolidColorBrush(Color.FromArgb(bgAlpha, 0x0B, 0x1A, 0x28));
            GlassRootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(borderAlpha, 0x4D, 0x7B, 0x99));

            WeatherChip.Background = new SolidColorBrush(Color.FromArgb(chipBgAlpha, 0x00, 0x20, 0x33));
            WeatherChip.BorderBrush = new SolidColorBrush(Color.FromArgb(chipBorderAlpha, 0x4A, 0x80, 0xA3));
            WeatherChip.BorderThickness = new Thickness(1);

            HardwareChip.Background = new SolidColorBrush(Color.FromArgb(chipBgAlpha, 0x00, 0x20, 0x33));
            HardwareChip.BorderBrush = new SolidColorBrush(Color.FromArgb(chipBorderAlpha, 0x4A, 0x80, 0xA3));
            HardwareChip.BorderThickness = new Thickness(1);

            WallpaperPanel.Background = new SolidColorBrush(Color.FromArgb(chipBgAlpha, 0x00, 0x00, 0x00));
            WallpaperPanel.BorderBrush = new SolidColorBrush(Color.FromArgb(chipBorderAlpha, 0x40, 0x6B, 0x87));
            WallpaperPanel.BorderThickness = new Thickness(1);
        }

        EnableAcrylicBlur();

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

    protected override void OnClosed(EventArgs e)
    {
        clockTimer.Stop(); hardwareTimer.Stop(); weatherTimer.Stop();
        base.OnClosed(e);
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
        catch (Exception ex)
        {
            Store.Log("Widget Hardware update warning: " + ex.Message);
        }
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
        catch (Exception ex)
        {
            Store.Log("Widget Weather update warning: " + ex.Message);
        }
    }

    public void UpdateWallpaperInfo()
    {
        try
        {
            currentPhoto = Store.CurrentDesktopPhoto();
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
        catch (Exception ex)
        {
            Store.Log("Widget Wallpaper info warning: " + ex.Message);
        }
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

        preferences = Store.UpdateSettings(p => { p.WidgetLeft = Left; p.WidgetTop = Top; });
    }

    void Window_LocationChanged(object sender, EventArgs e)
    {
        if (IsLoaded && WindowState == WindowState.Normal)
        {
            try
            {
                preferences = Store.UpdateSettings(p => { p.WidgetLeft = Left; p.WidgetTop = Top; });
            }
            catch (Exception ex)
            {
                Store.Log("Widget LocationChanged save warning: " + ex.Message);
            }
        }
    }

    void PinModeBtn_Click(object sender, RoutedEventArgs e)
    {
        var currentMode = Store.Settings.WidgetPinMode ?? "Desktop";
        preferences.WidgetPinMode = currentMode switch
        {
            "Desktop" => "Normal",
            "Normal" => "TopMost",
            _ => "Desktop"
        };
        preferences = Store.UpdateSettings(p => p.WidgetPinMode = preferences.WidgetPinMode);
        ApplyPinMode();
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        preferences = Store.UpdateSettings(p => p.ShowDesktopWidget = false);
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
            preferences = Store.UpdateSettings(p => { if (!p.Favorites.Remove(currentPhoto.Id)) p.Favorites.Add(currentPhoto.Id); });
            FavoriteBtn.Content = preferences.Favorites.Contains(currentPhoto.Id) ? "ستاره‌دار ⭐" : "علاقه‌مندی ❤️";
        }
        catch (Exception ex)
        {
            Store.Log("Widget Favorite_Click warning: " + ex.Message);
        }
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
        catch (Exception ex)
        {
            Store.Log("Widget SyncAccent_Click warning: " + ex.Message);
        }
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
        preferences = Store.UpdateSettings(p => p.WidgetPinMode = preferences.WidgetPinMode);
        ApplyPinMode();
    }

    void MenuPinNormal_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetPinMode = "Normal";
        preferences = Store.UpdateSettings(p => p.WidgetPinMode = preferences.WidgetPinMode);
        ApplyPinMode();
    }

    void MenuPinTopMost_Click(object sender, RoutedEventArgs e)
    {
        preferences.WidgetPinMode = "TopMost";
        preferences = Store.UpdateSettings(p => p.WidgetPinMode = preferences.WidgetPinMode);
        ApplyPinMode();
    }

    void MenuToggleWeather_Click(object sender, RoutedEventArgs e)
    {
        preferences = Store.UpdateSettings(p => p.WidgetShowWeather = !p.WidgetShowWeather);
        ApplyPreferences();
    }

    void MenuToggleHardware_Click(object sender, RoutedEventArgs e)
    {
        preferences = Store.UpdateSettings(p => p.WidgetShowHardware = !p.WidgetShowHardware);
        ApplyPreferences();
    }

    void MenuToggleClock_Click(object sender, RoutedEventArgs e)
    {
        preferences = Store.UpdateSettings(p => p.WidgetShowClock = !p.WidgetShowClock);
        ApplyPreferences();
    }

    void MenuToggleWallpaper_Click(object sender, RoutedEventArgs e)
    {
        preferences = Store.UpdateSettings(p => p.WidgetShowWallpaperInfo = !p.WidgetShowWallpaperInfo);
        ApplyPreferences();
    }

    void MenuToggleActions_Click(object sender, RoutedEventArgs e)
    {
        preferences = Store.UpdateSettings(p => p.WidgetShowQuickActions = !p.WidgetShowQuickActions);
        ApplyPreferences();
    }

    void MenuOpacity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && TryParseOpacity(item.Tag?.ToString(), out var op))
        {
            preferences = Store.UpdateSettings(p => p.WidgetOpacity = op);
            ApplyPreferences();
        }
    }
    internal static bool TryParseOpacity(string? value, out double opacity) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out opacity);
}
