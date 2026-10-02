using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace SahandNama;

public static class TrayManager
{
    const uint NIM_ADD = 0x00000000;
    const uint NIM_MODIFY = 0x00000001;
    const uint NIM_DELETE = 0x00000002;
    const uint NIF_MESSAGE = 0x00000001;
    const uint NIF_ICON = 0x00000002;
    const uint NIF_TIP = 0x00000004;
    const uint NIF_INFO = 0x00000010;

    const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 1024;
    const int WM_LBUTTONUP = 0x0202;
    const int WM_LBUTTONDBLCLK = 0x0203;
    const int WM_RBUTTONUP = 0x0205;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    static extern IntPtr CopyIcon(IntPtr hIcon);

    static IntPtr currentHwnd = IntPtr.Zero;
    static IntPtr iconHandle = IntPtr.Zero;
    static bool isAdded = false;
    static ContextMenu? trayMenu;

    public static void Initialize(IntPtr hwnd)
    {
        currentHwnd = hwnd;
        if (isAdded) return;

        try
        {
            if (iconHandle == IntPtr.Zero)
            {
                var stream = Application.GetResourceStream(new Uri("pack://application:,,,/SahandNama;component/Assets/SahandNama.ico"))?.Stream;
                if (stream != null)
                {
                    using var icon = new System.Drawing.Icon(stream, 32, 32);
                    iconHandle = CopyIcon(icon.Handle);
                }

                if (iconHandle == IntPtr.Zero)
                {
                    var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                        if (icon != null)
                        {
                            iconHandle = CopyIcon(icon.Handle);
                        }
                    }
                }
            }

            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = iconHandle,
                szTip = "سهند نما • مدیریت تصاویر پس‌زمینه و لاک‌اسکرین"
            };

            isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
            BuildContextMenu();
        }
        catch { }
    }

    static void BuildContextMenu()
    {
        trayMenu = new ContextMenu
        {
            FlowDirection = FlowDirection.RightToLeft
        };

        var openItem = new MenuItem { Header = "🖼️ نمایش سهند نما (پنجره اصلی)", FontWeight = FontWeights.Bold };
        openItem.Click += (_, _) => App.ShowMainWindow();
        trayMenu.Items.Add(openItem);

        trayMenu.Items.Add(new Separator());

        var nextItem = new MenuItem { Header = "🎲 تصویر بعدی والپیپر (Win+Alt+W)" };
        nextItem.Click += async (_, _) =>
        {
            try
            {
                await new WallpaperEngine().UpdateAsync();
                App.UpdateWidgetInfo();
                ShowBalloon("سهند نما", "تصویر پس‌زمینه با موفقیت به‌روزرسانی شد.");
            }
            catch (Exception ex)
            {
                ShowBalloon("خطا در به‌روزرسانی", ex.Message);
            }
        };
        trayMenu.Items.Add(nextItem);

        var favItem = new MenuItem { Header = "❤️ افزودن تصویر فعلی به علاقه‌مندی‌ها (Win+Alt+S)" };
        favItem.Click += (_, _) =>
        {
            try
            {
                var prefs = Store.Settings;
                var archive = Store.Read(Path.Combine(Store.Root, "archive.json"), new List<Photo>());
                var current = archive.FirstOrDefault(p => File.Exists(p.FilePath));
                if (current != null && !prefs.Favorites.Contains(current.Id))
                {
                    prefs.Favorites.Add(current.Id);
                    Store.Save(prefs);
                    App.UpdateWidgetInfo();
                    ShowBalloon("علاقه‌مندی‌ها", $"تصویر «{current.Title}» به لیست علاقه‌مندی‌ها اضافه شد.");
                }
            }
            catch { }
        };
        trayMenu.Items.Add(favItem);

        var widgetItem = new MenuItem { Header = "🕒 نمایش / پنهان‌سازی ویجت شیشه‌ای دسکتاپ" };
        widgetItem.Click += (_, _) => App.ToggleWidget();
        trayMenu.Items.Add(widgetItem);

        var accentItem = new MenuItem { Header = "🎨 هماهنگ‌سازی رنگ تم ویندوز (Accent Color)" };
        accentItem.Click += (_, _) =>
        {
            try
            {
                var archive = Store.Read(Path.Combine(Store.Root, "archive.json"), new List<Photo>());
                var current = archive.FirstOrDefault(p => File.Exists(p.FilePath));
                if (current != null && File.Exists(current.FilePath))
                {
                    WindowsIntegration.SyncWindowsAccentColor(current.FilePath);
                    ShowBalloon("رنگ تم ویندوز", "رنگ تم ویندوز با تصویر پس‌زمینه هماهنگ شد.");
                }
            }
            catch { }
        };
        trayMenu.Items.Add(accentItem);

        var iconsItem = new MenuItem { Header = "🖥️ پنهان / نمایان‌سازی آیکون‌های دسکتاپ" };
        iconsItem.Click += (_, _) =>
        {
            try { WindowsIntegration.ToggleDesktopIcons(); }
            catch { }
        };
        trayMenu.Items.Add(iconsItem);

        trayMenu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = "⚙️ تنظیمات و منابع تصویر…" };
        settingsItem.Click += (_, _) => App.ShowMainWindow();
        trayMenu.Items.Add(settingsItem);

        var exitItem = new MenuItem { Header = "🚪 خروج کامل از برنامه" };
        exitItem.Click += (_, _) => App.ExitApplication();
        trayMenu.Items.Add(exitItem);
    }

    public static void HandleTrayMessage(int lParam)
    {
        if (lParam == WM_LBUTTONDBLCLK || lParam == WM_LBUTTONUP)
        {
            App.ShowMainWindow();
        }
        else if (lParam == WM_RBUTTONUP)
        {
            if (trayMenu != null)
            {
                SetForegroundWindow(currentHwnd);
                trayMenu.IsOpen = true;
            }
        }
    }

    public static void ShowBalloon(string title, string message)
    {
        if (!isAdded || currentHwnd == IntPtr.Zero) return;
        try
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = currentHwnd,
                uID = 1001,
                uFlags = NIF_INFO,
                szInfoTitle = title.Length > 63 ? title[..63] : title,
                szInfo = message.Length > 255 ? message[..255] : message,
                dwInfoFlags = 0x00000001 // NIIF_INFO
            };
            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }
        catch { }
    }

    public static void Dispose()
    {
        if (isAdded && currentHwnd != IntPtr.Zero)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = currentHwnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            isAdded = false;
        }
        if (iconHandle != IntPtr.Zero)
        {
            DestroyIcon(iconHandle);
            iconHandle = IntPtr.Zero;
        }
    }
}
