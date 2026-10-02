using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace BingWallpaperPro;

public partial class DesktopWidgetWindow : Window
{
    readonly DispatcherTimer timer;
    Preferences preferences;
    Photo? currentPhoto;

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

        UpdateClock();
        UpdateWallpaperInfo();

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateClock();
        timer.Start();
    }

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        EnableAcrylicBlur();
    }

    void EnableAcrylicBlur()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
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

    void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm:ss");
        PersianDateText.Text = PersianDateHelper.GetFormattedPersianDate(now);
        GregorianDateText.Text = now.ToString("dd MMM yyyy");
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
        }
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

    void PinBtn_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinBtn.Opacity = Topmost ? 1.0 : 0.55;
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
}
