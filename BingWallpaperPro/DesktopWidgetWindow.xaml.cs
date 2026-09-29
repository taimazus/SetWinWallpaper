using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BingWallpaperPro;

public partial class DesktopWidgetWindow : Window
{
    readonly DispatcherTimer timer;
    Preferences preferences;

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

    void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm:ss");
        PersianDateText.Text = PersianDateHelper.GetFormattedPersianDate(now);
    }

    public void UpdateWallpaperInfo()
    {
        try
        {
            var archive = Store.Read(Path.Combine(Store.Root, "archive.json"), new List<Photo>());
            var current = archive.FirstOrDefault(p => File.Exists(p.FilePath));
            if (current != null)
            {
                WallpaperTitleText.Text = string.IsNullOrWhiteSpace(current.Title) ? "منظره روز ویندوز" : current.Title;
                WallpaperCreditText.Text = string.IsNullOrWhiteSpace(current.Copyright) ? $"{current.Source} • {current.Market}" : current.Copyright;
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

    void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        preferences.ShowDesktopWidget = false;
        Store.Save(preferences);
        Close();
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
            WallpaperTitleText.Text = "خطا در دریافت: " + ex.Message;
        }
    }

    void Favorite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var archive = Store.Read(Path.Combine(Store.Root, "archive.json"), new List<Photo>());
            var current = archive.FirstOrDefault(p => File.Exists(p.FilePath));
            if (current != null)
            {
                if (!preferences.Favorites.Contains(current.Id))
                {
                    preferences.Favorites.Add(current.Id);
                    Store.Save(preferences);
                    WallpaperTitleText.Text = "❤️ به علاقه‌مندی‌ها اضافه شد!";
                }
            }
        }
        catch { }
    }

    void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        var main = Application.Current.MainWindow;
        if (main != null)
        {
            main.Show();
            main.WindowState = WindowState.Normal;
            main.Activate();
        }
    }
}
