using System.IO;
using System.Runtime.InteropServices;

namespace SahandNama;

// SCM service: download only. User wallpaper changes belong in the interactive scheduled task.
public static class DownloadService
{
    public const string Name = "BingWallpaperProFeed";
    static ManualResetEventSlim Stopped = new(false);
    static CancellationTokenSource Cancellation = new();
    static readonly ServiceMain MainCallback = ServiceEntry;
    static readonly Handler HandlerCallback = Control;
    static IntPtr handle;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void ServiceMain(int count, IntPtr args);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Handler(uint control);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Entry { [MarshalAs(UnmanagedType.LPWStr)] public string? Name; public ServiceMain? Main; }
    [StructLayout(LayoutKind.Sequential)] struct Status { public uint Type, State, Accepted, ExitCode, SpecificExit, Checkpoint, WaitHint; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool StartServiceCtrlDispatcher([In] Entry[] table);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr RegisterServiceCtrlHandler(string name, Handler handler);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool SetServiceStatus(IntPtr handle, ref Status status);

    static void Report(uint state, uint code = 0)
    {
        if (handle == IntPtr.Zero) return;
        var status = new Status
        {
            Type = 0x10,
            State = state,
            Accepted = state == 4 ? 5u : 0u,
            ExitCode = code,
            WaitHint = state is 2 or 3 ? 15000u : 0u
        };
        SetServiceStatus(handle, ref status);
    }

    public static int Run() => StartServiceCtrlDispatcher([new Entry { Name = Name, Main = MainCallback }, new Entry()]) ? 0 : Marshal.GetLastWin32Error();

    static void Control(uint control)
    {
        if (control is 1 or 5)
        {
            Report(3);
            try { Cancellation.Cancel(); } catch { }
            Stopped.Set();
        }
    }

    static void ServiceEntry(int count, IntPtr args)
    {
        handle = RegisterServiceCtrlHandler(Name, HandlerCallback);
        if (handle == IntPtr.Zero) return;
        Stopped.Reset();
        if (Cancellation.IsCancellationRequested)
        {
            try { Cancellation.Dispose(); } catch { }
            Cancellation = new CancellationTokenSource();
        }
        Report(4);
        Store.Log("Download service started successfully.", Store.SharedRoot);

        try
        {
            while (!Stopped.IsSet && !Cancellation.IsCancellationRequested)
            {
                var success = false;
                try
                {
                    var catalog = new SourceCatalog(Store.SharedRoot);
                    var countSynced = catalog.SyncAllOnlineSourcesAsync("UHD", Cancellation.Token).GetAwaiter().GetResult();
                    Store.Log($"سرویس فید: همگام‌سازی مخزن مشترک با موفقیت انجام شد ({countSynced} تصویر).", Store.SharedRoot);
                    success = true;
                }
                catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Store.Log($"سرویس فید: بروز خطا در همگام‌سازی — {ex.Message}", Store.SharedRoot);
                }

                var waitTime = success ? TimeSpan.FromHours(12) : TimeSpan.FromMinutes(10);
                try { Stopped.Wait(waitTime, Cancellation.Token); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (Exception ex)
        {
            Store.Log($"Download service unexpected exit: {ex}", Store.SharedRoot);
        }
        finally
        {
            Report(1);
            Store.Log("Download service stopped.", Store.SharedRoot);
        }
    }
}
