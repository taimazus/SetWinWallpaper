using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BingWallpaperPro;

public static class DesktopPinning
{
    static readonly IntPtr HWND_BOTTOM = new(1);
    static readonly IntPtr HWND_NOTOPMOST = new(-2);
    static readonly IntPtr HWND_TOPMOST = new(-1);

    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static void ApplyPinMode(Window window, string pinMode)
    {
        if (window == null || !window.IsLoaded) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        switch (pinMode?.ToLowerInvariant())
        {
            case "desktop":
                window.Topmost = false;
                SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                break;

            case "topmost":
                window.Topmost = true;
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                break;

            case "normal":
            default:
                window.Topmost = false;
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                break;
        }
    }

    public static void SendToBottom(Window window)
    {
        if (window == null || !window.IsLoaded) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
}
