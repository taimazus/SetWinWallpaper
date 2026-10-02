using System.Windows;
using Application = System.Windows.Application;

namespace BingWallpaperPro;

public partial class App : Application
{
    public static bool IsExiting { get; set; }
    public static DesktopWidgetWindow? ActiveWidget { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--service"))
        {
            Shutdown(await Task.Run(() => DownloadService.Run()));
            return;
        }

        if (e.Args.Contains("--update") || e.Args.Contains("--update-quiet"))
        {
            try
            {
                await new WallpaperEngine().UpdateAsync();
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--sync-all"))
        {
            try
            {
                var synced = await new SourceCatalog().SyncAllOnlineSourcesAsync();
                Store.Log($"Sync-All completed with {synced} images.");
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--install-schedule"))
        {
            try
            {
                var time = e.Args.FirstOrDefault(a => a.Length == 5 && a[2] == ':' && char.IsDigit(a[0])) ?? Store.Settings.DailyTime;
                var res = await WindowsIntegration.InstallScheduleAsync(time);
                Store.Log(res);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--remove-schedule"))
        {
            try
            {
                var res = await WindowsIntegration.RemoveScheduleAsync();
                Store.Log(res);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--install-service"))
        {
            try
            {
                var res = await WindowsIntegration.InstallServiceAsync();
                Store.Log(res);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--remove-service"))
        {
            try
            {
                var res = await WindowsIntegration.RemoveServiceAsync();
                Store.Log(res);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--start-service"))
        {
            try
            {
                var res = await WindowsIntegration.StartServiceAsync();
                Store.Log(res);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--stop-service"))
        {
            try
            {
                var res = await WindowsIntegration.StopServiceAsync();
                Store.Log(res);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--diagnose"))
        {
            try
            {
                var report = await Diagnostics.RunAsync(true);
                Store.Log(report.ToText());
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--repair"))
        {
            try
            {
                var result = await Diagnostics.AutoRepairAllAsync();
                Store.Log(result.Summary + "\n" + result.UpdatedReport.ToText());
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--widget"))
        {
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                ActiveWidget = new DesktopWidgetWindow();
                ActiveWidget.Show();
                var handle = new System.Windows.Interop.WindowInteropHelper(ActiveWidget).Handle;
                TrayManager.Initialize(handle);
                return;
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
                return;
            }
        }

        if (e.Args.Contains("--toggle-icons"))
        {
            try
            {
                WindowsIntegration.ToggleDesktopIcons();
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Store.Log(ex.ToString());
                Shutdown(1);
            }
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        MainWindow = new MainWindow();
        MainWindow.Show();

        if (Store.Settings.ShowDesktopWidget)
        {
            try
            {
                ActiveWidget = new DesktopWidgetWindow();
                ActiveWidget.Show();
            }
            catch { }
        }
    }

    public static void ShowMainWindow()
    {
        var app = Current;
        if (app == null) return;
        app.Dispatcher.Invoke(() =>
        {
            if (app.MainWindow == null || !app.MainWindow.IsLoaded)
            {
                app.MainWindow = new MainWindow();
            }
            app.MainWindow.Show();
            if (app.MainWindow.WindowState == WindowState.Minimized)
            {
                app.MainWindow.WindowState = WindowState.Normal;
            }
            app.MainWindow.Activate();
            app.MainWindow.Focus();
        });
    }

    public static void ToggleWidget()
    {
        var app = Current;
        if (app == null) return;
        app.Dispatcher.Invoke(() =>
        {
            if (ActiveWidget == null || !ActiveWidget.IsLoaded)
            {
                ActiveWidget = new DesktopWidgetWindow();
                ActiveWidget.Show();
                var prefs = Store.Settings;
                prefs.ShowDesktopWidget = true;
                Store.Save(prefs);
            }
            else if (ActiveWidget.IsVisible)
            {
                ActiveWidget.Hide();
                var prefs = Store.Settings;
                prefs.ShowDesktopWidget = false;
                Store.Save(prefs);
            }
            else
            {
                ActiveWidget.Show();
                ActiveWidget.Activate();
                var prefs = Store.Settings;
                prefs.ShowDesktopWidget = true;
                Store.Save(prefs);
            }
        });
    }

    public static void UpdateWidgetInfo()
    {
        var app = Current;
        if (app == null) return;
        app.Dispatcher.Invoke(() =>
        {
            if (ActiveWidget != null && ActiveWidget.IsLoaded && ActiveWidget.IsVisible)
            {
                ActiveWidget.UpdateWallpaperInfo();
            }
        });
    }

    public static void ApplyWidgetPreferences()
    {
        var app = Current;
        if (app == null) return;
        app.Dispatcher.Invoke(() =>
        {
            if (ActiveWidget != null && ActiveWidget.IsLoaded && ActiveWidget.IsVisible)
            {
                ActiveWidget.ApplyPreferences();
            }
        });
    }

    public static void ExitApplication()
    {
        IsExiting = true;
        TrayManager.Dispose();
        var app = Current;
        if (app == null) return;
        app.Dispatcher.Invoke(() =>
        {
            ActiveWidget?.Close();
            app.MainWindow?.Close();
            app.Shutdown(0);
        });
    }
}
