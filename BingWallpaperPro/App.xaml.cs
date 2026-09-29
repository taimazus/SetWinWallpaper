using System.Windows;

namespace BingWallpaperPro;

public partial class App : Application
{
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
                ShutdownMode = ShutdownMode.OnLastWindowClose;
                var widget = new DesktopWidgetWindow();
                widget.Show();
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

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}

