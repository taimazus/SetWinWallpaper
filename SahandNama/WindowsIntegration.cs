using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace SahandNama;

public static class WindowsIntegration
{
    public const string TaskPrefix = "BingWallpaperPro-Daily-";
    public static string TaskName => TaskPrefix + WindowsIdentity.GetCurrent().User!.Value;
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SystemParametersInfo(uint action, uint param, string value, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SystemParametersInfoW")]
    static extern bool GetParameters(uint action, uint param, StringBuilder value, uint flags);
    public static string QuotePS(string value) => "'" + value.Replace("'", "''") + "'";
    static string PowerShell => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");

    public static async Task<string> RunPowerShellAsync(string script, int timeoutSeconds = 120)
    {
        var info = new ProcessStartInfo(PowerShell) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-OutputFormat"); info.ArgumentList.Add("Text"); info.ArgumentList.Add("-EncodedCommand");
        // EncodedCommand otherwise serializes error/progress streams as CLIXML.
        var wrapped = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.Encoding]::UTF8; try { & {\n" + script + "\n} } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped)));
        using var process = Process.Start(info) ?? throw new IOException("Cannot start PowerShell.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(cancellation.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Windows operation timed out."); }
        var text = await output; var errors = await error;
        if (process.ExitCode != 0) throw new InvalidOperationException(errors.Trim().Length > 0 ? errors.Trim() : text);
        return text.Trim();
    }

    public static void SetDesktop(string path, string fit)
    {
        Store.LoadImage(path, 32);
        using var policy = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
        if (policy?.GetValue("Wallpaper") is string forced && forced.Length > 0) throw new InvalidOperationException("پس‌زمینه توسط سیاست سازمانی مدیریت می‌شود.");
        using var desktop = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
        var backup = Path.Combine(Store.Root, "desktop-backup.json");
        if (!File.Exists(backup))
        {
            var current = new StringBuilder(32768);
            if (!GetParameters(0x73, (uint)current.Capacity, current, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            Store.Write(backup, new DesktopBackup(current.ToString(), desktop.GetValue("WallpaperStyle")?.ToString() ?? "10", desktop.GetValue("TileWallpaper")?.ToString() ?? "0"));
        }
        var style = fit switch { "Fit" => "6", "Stretch" => "2", "Center" => "0", "Span" => "22", "Fill" => "10", _ => throw new ArgumentException("Unknown fit.") };
        var previousStyle = desktop.GetValue("WallpaperStyle")?.ToString() ?? "10";
        var previousTile = desktop.GetValue("TileWallpaper")?.ToString() ?? "0";
        desktop.SetValue("WallpaperStyle", style); desktop.SetValue("TileWallpaper", "0");
        if (!SystemParametersInfo(20, 0, Path.GetFullPath(path), 3))
        {
            var error = Marshal.GetLastWin32Error();
            desktop.SetValue("WallpaperStyle", previousStyle); desktop.SetValue("TileWallpaper", previousTile);
            throw new Win32Exception(error);
        }
        Store.Log("Desktop set: " + path);
    }
    public sealed record DesktopBackup(string Path, string Style, string Tile);
    public static void RestoreDesktop()
    {
        var backup = Store.Read<DesktopBackup?>(Path.Combine(Store.Root, "desktop-backup.json"), null) ?? throw new InvalidOperationException("نسخه پشتیبان موجود نیست.");
        if (backup.Path.Length > 0 && !File.Exists(backup.Path)) throw new FileNotFoundException("تصویر قبلی دیگر موجود نیست.", backup.Path);
        using var desktop = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
        desktop.SetValue("WallpaperStyle", backup.Style); desktop.SetValue("TileWallpaper", backup.Tile);
        if (!SystemParametersInfo(20, 0, backup.Path, 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var currentMetadata = Path.Combine(Store.Root, "current-desktop.json");
        if (File.Exists(currentMetadata)) File.Delete(currentMetadata);
    }
    public static async Task<string> SetLockScreenAsync(string path)
    {
        Store.LoadImage(path, 32);
        var result = await RunPowerShellAsync(LockScreenScript(Path.GetFullPath(path)));
        Store.Log("Lock screen: " + result);
        return result;
    }

    internal static string LockScreenScript(string path) => $$"""
            $fullPath = {{QuotePS(path)}}
            $ok = $false
            $lastError = ''

            # Strategy 1: WinRT UserProfilePersonalizationSettings / LockScreen API
            try {
                Add-Type -AssemblyName System.Runtime.WindowsRuntime -ErrorAction SilentlyContinue
                $storageFileType = [Type]::GetType('Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime')
                $userProfileType = [Type]::GetType('Windows.System.UserProfile.UserProfilePersonalizationSettings, Windows.System.UserProfile, ContentType=WindowsRuntime')
                $lockScreenType = [Type]::GetType('Windows.System.UserProfile.LockScreen, Windows.System.UserProfile, ContentType=WindowsRuntime')
                $extType = [System.WindowsRuntimeSystemExtensions]
                
                if ($storageFileType -and $extType) {
                    $asTaskGeneric = $extType.GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
                    $asTaskAction = $extType.GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and !$_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' } | Select-Object -First 1

                    $getFileAsyncMethod = $storageFileType.GetMethod('GetFileFromPathAsync', [Type[]]@([string]))
                    if ($getFileAsyncMethod -and $asTaskGeneric) {
                        $getFileOp = $getFileAsyncMethod.Invoke($null, @($fullPath))
                        $getFileTask = $asTaskGeneric.MakeGenericMethod($storageFileType).Invoke($null, @($getFileOp))
                        $file = $getFileTask.GetAwaiter().GetResult()

                        if ($userProfileType) {
                            $isSupportedMethod = $userProfileType.GetMethod('IsSupported', [Type[]]@())
                            $isSupported = if ($isSupportedMethod) { [bool]$isSupportedMethod.Invoke($null, @()) } else { $false }
                            if ($isSupported) {
                                $currentProp = $userProfileType.GetProperty('Current')
                                $currentInstance = if ($currentProp) { $currentProp.GetValue($null) } else { $null }
                                $trySetMethod = $userProfileType.GetMethod('TrySetLockScreenImageAsync', [Type[]]@($storageFileType))
                                if ($currentInstance -and $trySetMethod) {
                                    try {
                                        $trySetOp = $trySetMethod.Invoke($currentInstance, @($file))
                                        $trySetTask = $asTaskGeneric.MakeGenericMethod([bool]).Invoke($null, @($trySetOp))
                                        $ok = [bool]$trySetTask.GetAwaiter().GetResult()
                                    } catch { $lastError = $_.Exception.Message }
                                }
                            }
                        }

                        if (!$ok -and $lockScreenType -and $asTaskAction) {
                            $setImageFileMethod = $lockScreenType.GetMethod('SetImageFileAsync', [Type[]]@($storageFileType))
                            if ($setImageFileMethod) {
                                $actionOp = $setImageFileMethod.Invoke($null, @($file))
                                $actionTask = $asTaskAction.Invoke($null, @($actionOp))
                                $actionTask.GetAwaiter().GetResult()
                                $ok = $true
                            }
                        }
                    }
                }
            } catch {
                $lastError = $_.Exception.Message
            }

            if (!$ok) { throw ('Windows lock screen API did not accept the image. Use the managed policy helper on supported editions. ' + $lastError) }

            if ($ok) {
                Stop-Process -Name LockApp -Force -ErrorAction SilentlyContinue
                'Lock screen updated successfully.'
            }
            """;

    public const string ServiceName = "BingWallpaperProFeed";

    public static (string Executable, string Arguments) GetUpdateCommandLine()
        => GetUpdateCommandLine(AppContext.BaseDirectory, Environment.ProcessPath,
            System.Reflection.Assembly.GetEntryAssembly() == typeof(WindowsIntegration).Assembly);

    internal static (string Executable, string Arguments) GetUpdateCommandLine(string baseDir, string? processPath, bool runningApplication)
    {
        if (runningApplication && !string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath) &&
            processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            !processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return (Path.GetFullPath(processPath), "--update");
        var exePath = Path.Combine(baseDir, "SahandNama.exe");
        if (File.Exists(exePath)) return (Path.GetFullPath(exePath), "--update");

        var legacyExe = Path.Combine(baseDir, "BingWallpaperPro.exe");
        if (File.Exists(legacyExe)) return (Path.GetFullPath(legacyExe), "--update");

        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            if (processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return (Path.GetFullPath(processPath), "--update");
            }
        }

        var dllPath = Path.Combine(baseDir, "SahandNama.dll");
        if (File.Exists(dllPath))
        {
            var dotnetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
            if (!File.Exists(dotnetPath)) dotnetPath = "dotnet.exe";
            return (dotnetPath, $"exec \"{Path.GetFullPath(dllPath)}\" --update");
        }

        var legacyDll = Path.Combine(baseDir, "BingWallpaperPro.dll");
        if (File.Exists(legacyDll))
        {
            var dotnetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
            if (!File.Exists(dotnetPath)) dotnetPath = "dotnet.exe";
            return (dotnetPath, $"exec \"{Path.GetFullPath(legacyDll)}\" --update");
        }

        return (Path.GetFullPath(processPath ?? "SahandNama.exe"), "--update");
    }

    public static async Task<string> RunElevatedPowerShellAsync(string script, int timeoutSeconds = 60)
    {
        var wrapped = "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.Encoding]::UTF8; try { " + script + " } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped));
        var psi = new ProcessStartInfo(PowerShell)
        {
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var proc = Process.Start(psi) ?? throw new IOException("امکان اجرای فرآیند با دسترسی Administrator فراهم نشد.");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await proc.WaitForExitAsync(cts.Token);
            if (proc.ExitCode != 0) throw new InvalidOperationException($"عملیات سیستمی نیازمند تأیید دسترسی Administrator است (کد خروجی: {proc.ExitCode}).");
            return "عملیات با موفقیت انجام شد.";
        }
        catch (OperationCanceledException)
        {
            proc.Kill(true);
            throw new TimeoutException("زمان اجرای عملیات سیستمی به پایان رسید.");
        }
    }

    public static async Task<string> InstallScheduleAsync(string time)
    {
        try { return await RunPowerShellAsync(ScheduleScript(time), 45); }
        catch (InvalidOperationException ex) when (IsAccessDenied(ex.Message))
        {
            throw new InvalidOperationException("ویندوز اجازهٔ ثبت یا اصلاح زمان‌بندی را نداد. ممکن است مجوز زمان‌بندی قبلی یا سیاست سازمانی مانع باشد. برای اصلاح مجوزها با مدیر سیستم تماس بگیرید یا برنامه را با Run as administrator اجرا کنید. زمان‌بندی برای حسابی ثبت می‌شود که برنامه را اجرا کرده است.", ex);
        }
    }

    internal static bool IsAccessDenied(string message) =>
        message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Access denied", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("0x80070005", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("E_ACCESSDENIED", StringComparison.OrdinalIgnoreCase);

    internal static string ScheduleScript(string time)
    {
        if (!TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
            throw new ArgumentException("ساعت باید با ارقام انگلیسی به شکل 09:00 باشد.");

        var (exe, args) = GetUpdateCommandLine();
        args += " --scheduled";
        var workingDir = AppContext.BaseDirectory;
        var hour = parsedTime.Hour;
        var minute = parsedTime.Minute;

        var script = $$"""
            $user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
            $exe = {{QuotePS(exe)}}
            $args = {{QuotePS(args)}}
            $workDir = {{QuotePS(workingDir)}}
            $taskName = {{QuotePS(TaskName)}}

            $action = New-ScheduledTaskAction -Execute $exe -Argument $args -WorkingDirectory $workDir
            $triggerTime = (Get-Date -Hour {{hour}} -Minute {{minute}} -Second 0)
            $daily = New-ScheduledTaskTrigger -Daily -At $triggerTime
            $logon = New-ScheduledTaskTrigger -AtLogOn -User $user

            $principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited

            $settings = New-ScheduledTaskSettingsSet `
                -StartWhenAvailable `
                -MultipleInstances IgnoreNew `
                -ExecutionTimeLimit (New-TimeSpan -Minutes 20) `
                -RestartCount 24 `
                -RestartInterval (New-TimeSpan -Hours 1) `
                -AllowStartIfOnBatteries `
                -DontStopIfGoingOnBatteries `
                -WakeToRun:$false

            Register-ScheduledTask -TaskName $taskName -Action $action -Trigger @($daily, $logon) -Principal $principal -Settings $settings -Force | Out-Null
            "زمان‌بندی روزانه در ساعت {{time}} و هنگام ورود برای کاربر $user با موفقیت ثبت شد."
            """;

        return script;
    }

    internal static string RemoveScheduleScript() => $$"""
        $taskName = {{QuotePS(TaskName)}}
        $task = Get-ScheduledTask -TaskPath '\' | Where-Object { $_.TaskName -eq $taskName }
        if ($task) { $task | Unregister-ScheduledTask -Confirm:$false -ErrorAction Stop }
        'Schedule removed.'
        """;

    public static Task<string> RemoveScheduleAsync() => RunPowerShellAsync(RemoveScheduleScript());

    public static string ServiceManagementScript(string action, string? sourceRoot = null)
    {
        if (action is not ("Install" or "Remove")) throw new ArgumentException("Unknown service action.");
        using var stream = typeof(WindowsIntegration).Assembly.GetManifestResourceStream("SahandNama.Scripts.Manage-Service.ps1") ?? throw new IOException("Embedded service helper is missing.");
        using var reader = new StreamReader(stream);
        var executable = sourceRoot == null ? GetUpdateCommandLine().Executable : Path.Combine(sourceRoot, "SahandNama.exe");
        return "& {\n" + reader.ReadToEnd() + "\n} -Action " + action + " -SourceRoot " + QuotePS(sourceRoot ?? AppContext.BaseDirectory) + " -SourceExe " + QuotePS(executable);
    }
    public static Task<string> InstallServiceAsync() => RunElevatedPowerShellAsync(ServiceManagementScript("Install"));
    public static Task<string> RemoveServiceAsync() => RunElevatedPowerShellAsync(ServiceManagementScript("Remove"));

    public static async Task<string> StartServiceAsync()
    {
        var script = $$"""
            $serviceName = '{{ServiceName}}'
            Start-Service -Name $serviceName -ErrorAction Stop
            "سرویس $serviceName روشن شد."
            """;
        return await RunElevatedPowerShellAsync(script);
    }

    public static async Task<string> StopServiceAsync()
    {
        var script = $$"""
            $serviceName = '{{ServiceName}}'
            Stop-Service -Name $serviceName -Force -ErrorAction Stop
            "سرویس $serviceName متوقف شد."
            """;
        return await RunElevatedPowerShellAsync(script);
    }

    public static bool IsWindowsServer()
    {
        try
        {
            using var version = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var edition = version?.GetValue("EditionID")?.ToString() ?? "";
            var product = version?.GetValue("ProductName")?.ToString() ?? "";
            var installation = version?.GetValue("InstallationType")?.ToString() ?? "";
            return edition.Contains("Server", StringComparison.OrdinalIgnoreCase) ||
                   product.Contains("Server", StringComparison.OrdinalIgnoreCase) ||
                   installation.Contains("Server", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static string GetServerHostName() => Environment.MachineName;
    public static string GetDefaultServerSharePath(string shareName = "Wallpapers") => $@"\\{Environment.MachineName}\{shareName}";

    public static async Task<string> CreateOrUpdateSmbShareAsync(string shareName = "Wallpapers", string? folderPath = null)
    {
        folderPath ??= Store.Root;
        Directory.CreateDirectory(folderPath);

        var escapedPath = folderPath.Replace("'", "''");
        var escapedShare = shareName.Replace("'", "''");

        var script = $$"""
            $shareName = '{{escapedShare}}'
            $targetPath = '{{escapedPath}}'

            # 1. Create or update Windows SMB Share
            $existing = Get-SmbShare -Name $shareName -ErrorAction SilentlyContinue
            if ($existing) {
                if ($existing.Path -ne $targetPath) {
                    throw 'An existing share uses another path. Choose a new share name or migrate it explicitly.'
                }
            } else {
                New-SmbShare -Name $shareName -Path $targetPath -ReadAccess 'Everyone' -FolderEnumerationMode AccessBased -ErrorAction Stop | Out-Null
            }

            # 2. Grant NTFS read permissions to Everyone & Authenticated Users
            try {
                $acl = Get-Acl -Path $targetPath
                $rule1 = New-Object System.Security.AccessControl.FileSystemAccessRule('Everyone', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
                $rule2 = New-Object System.Security.AccessControl.FileSystemAccessRule('Authenticated Users', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
                $acl.AddAccessRule($rule1)
                $acl.AddAccessRule($rule2)
                Set-Acl -Path $targetPath -AclObject $acl
            } catch { }

            $machine = $env:COMPUTERNAME
            "\\$machine\$shareName"
            """;

        try
        {
            var unc = await RunPowerShellAsync(script, 45);
            Store.Log($"Windows SMB Share '{shareName}' created/verified at '{folderPath}'.");
            return unc.Trim();
        }
        catch (InvalidOperationException ex) when (!ex.Message.Contains("An existing share uses another path", StringComparison.Ordinal))
        {
            // Elevation fallback with UAC
            var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + script));
            var psi = new ProcessStartInfo(PowerShell)
            {
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var proc = Process.Start(psi) ?? throw new IOException("امکان اجرای فرآیند اشتراک‌گذاری با دسترسی مدیر فراهم نشد.");
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0) throw new InvalidOperationException("ایجاد اشتراک شبکه در ویندوز نیازمند تایید دسترسی Administrator است.");
            return $@"\\{Environment.MachineName}\{shareName}";
        }
    }

    public static async Task<bool> IsSmbShareActiveAsync(string shareName = "Wallpapers")
    {
        try
        {
            var script = $$"""
                if (Get-SmbShare -Name '{{shareName.Replace("'", "''")}}' -ErrorAction SilentlyContinue) { 'true' } else { 'false' }
                """;
            var res = await RunPowerShellAsync(script, 15);
            return res.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static async Task<string> DiagnoseAsync()
    {
        var result = new StringBuilder();
        using var version = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        result.AppendLine($"Windows: {version?.GetValue("ProductName")} / {version?.GetValue("EditionID")} / Build {version?.GetValue("CurrentBuildNumber")}");
        result.AppendLine($"Session: {Process.GetCurrentProcess().SessionId} | User: {WindowsIdentity.GetCurrent().Name}");
        result.AppendLine("Server Core has no supported desktop UI; Server Desktop Experience requires edition/policy verification.");
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var key in new[] { @"SOFTWARE\Policies\Microsoft\Windows\Personalization", @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop" })
        {
            using var policy = hive.OpenSubKey(key);
            if (policy == null) continue;
            foreach (var name in policy.GetValueNames()) result.AppendLine($"Policy: {hive.Name}\\{key}\\{name} = {policy.GetValue(name)}");
        }
        result.AppendLine("Policies shown above may override local changes; the app does not delete them.");
        result.AppendLine("Spotlight package: " + (Directory.Exists(SpotlightRoot) ? "present" : "not found (may be unavailable on this OS)"));
        result.AppendLine("Spotlight reset only targets legacy ContentDeliveryManager settings, not every modern Spotlight provider.");
        try
        {
            result.AppendLine(await RunPowerShellAsync($$"""
                $task=Get-ScheduledTask -TaskName {{QuotePS(TaskName)}} -ErrorAction SilentlyContinue
                if ($task) { $task | Select-Object TaskName,State | Format-List | Out-String; $task | Get-ScheduledTaskInfo | Select-Object LastRunTime,LastTaskResult,NextRunTime | Format-List | Out-String } else { 'Daily task: not installed' }
                $service=Get-Service BingWallpaperProFeed -ErrorAction SilentlyContinue
                if ($service) { $service | Select-Object Name,Status,StartType | Format-List | Out-String } else { 'Download service: not installed' }
                """));
        }
        catch (Exception ex) { result.AppendLine(ex.Message); }
        var lastRun = Path.Combine(Store.Root, "last-run.json");
        if (File.Exists(lastRun)) result.AppendLine("Last update:\n" + File.ReadAllText(lastRun));
        return result.ToString();
    }
    static string SpotlightRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy");
    public static async Task<List<string>> CleanLegacyTasksAndFoldersAsync()
    {
        var logs = new List<string>();
        var script = """
            $legacyTasks = @('Bing Wallpaper Daily', 'BingWallpaperDaily', 'BingWallpaper_Enterprise')
            foreach ($t in $legacyTasks) {
                if (Get-ScheduledTask -TaskName $t -ErrorAction SilentlyContinue) {
                    Unregister-ScheduledTask -TaskName $t -Confirm:$false -ErrorAction Stop
                    Write-Output "تسک قدیمی '$t' با موفقیت از سیستم حذف شد."
                }
            }
            if (Test-Path 'C:\ProgramData\BingWallpaper') {
                Remove-Item -LiteralPath 'C:\ProgramData\BingWallpaper' -Recurse -Force -ErrorAction Stop
                Write-Output "پوشه و اسکریپت‌های منسوخ C:\ProgramData\BingWallpaper حذف شدند."
            }
            """;
        try
        {
            var outText = await RunPowerShellAsync(script, 30);
            if (!string.IsNullOrWhiteSpace(outText))
            {
                logs.AddRange(outText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }
        catch (Exception ex) { Store.Log("Legacy cleanup failed: " + ex.Message); throw; }
        return logs;
    }

    internal static List<string> BackupAndRemoveSpotlightSettings(string settings, string backup)
    {
        var errors = new List<string>();
        foreach (var file in Directory.EnumerateFiles(settings))
        {
            try { File.Copy(file, Path.Combine(backup, Path.GetFileName(file))); File.Delete(file); }
            catch (Exception ex) { errors.Add(Path.GetFileName(file) + ": " + ex.Message); }
        }
        return errors;
    }

    public static async Task<string> ResetSpotlightAsync()
    {
        var settings = Path.Combine(SpotlightRoot, "Settings");
        var assets = Path.Combine(SpotlightRoot, "LocalState", "Assets");
        if (!Directory.Exists(SpotlightRoot)) throw new InvalidOperationException("بسته Spotlight این حساب موجود نیست؛ بازنشانی برای این نسخه ویندوز قابل اجرا نیست.");

        var backup = Path.Combine(Store.Root, "Backups", "Spotlight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);

        var sb = new StringBuilder();
        var errors = new List<string>();
        sb.AppendLine("✓ شروع فرآیند جامع تعمیر و بازنشانی Windows Spotlight:");

        // 1. Backup and reset settings files
        if (Directory.Exists(settings))
        {
            var settingErrors = BackupAndRemoveSpotlightSettings(settings, backup);
            errors.AddRange(settingErrors);
            if (settingErrors.Count == 0) sb.AppendLine("✓ فایل‌های تنظیمات محلی و قفل‌های همگام‌سازی (Settings & Roaming Locks) پاک‌سازی شدند.");
        }

        // 2. Clean corrupted / 0-byte assets cache
        if (Directory.Exists(assets))
        {
            var deletedAssets = 0;
            foreach (var file in Directory.EnumerateFiles(assets))
            {
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.Length < 10000) { File.Delete(file); deletedAssets++; }
                }
                catch (Exception ex) { errors.Add(Path.GetFileName(file) + ": " + ex.Message); }
            }
            if (deletedAssets > 0) sb.AppendLine($"✓ تعداد {deletedAssets} فایل کش ناقص و معیوب از Assets حذف شد.");
        }

        // 3. Fix and enable ContentDeliveryManager Registry Keys
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager");
            if (key != null)
            {
                key.SetValue("ContentDeliveryAllowed", 1, RegistryValueKind.DWord);
                key.SetValue("RotatingLockScreenEnabled", 1, RegistryValueKind.DWord);
                key.SetValue("RotatingLockScreenOverlayEnabled", 1, RegistryValueKind.DWord);
                key.SetValue("SubscribedContent-338387Enabled", 1, RegistryValueKind.DWord);
                key.SetValue("SubscribedContent-338388Enabled", 1, RegistryValueKind.DWord);
                key.SetValue("SubscribedContent-338389Enabled", 1, RegistryValueKind.DWord);
            }
            sb.AppendLine("✓ کلیدهای فعال‌سازی Spotlight در رجیستری ویندوز (ContentDeliveryManager) تصحیح شدند.");
        }
        catch (Exception ex) { errors.Add("Registry: " + ex.Message); }

        // 4. Re-register ContentDeliveryManager AppX Package via PowerShell
        try
        {
            await RunPowerShellAsync("""
                $package = Get-AppxPackage -Name Microsoft.Windows.ContentDeliveryManager -ErrorAction Stop
                if (!$package) { throw 'ContentDeliveryManager package is unavailable.' }
                $package | Foreach-Object {
                    Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml" -ErrorAction Stop
                }
                """, 30);
            sb.AppendLine("✓ بسته نرم‌افزاری ContentDeliveryManager در ویندوز بازثبت (Re-register) شد.");
        }
        catch (Exception ex) { errors.Add("AppX: " + ex.Message); }

        if (errors.Count > 0)
        {
            var failure = "Spotlight reset incomplete; backup: " + backup + "\n" + string.Join("\n", errors);
            Store.Log(failure);
            throw new InvalidOperationException(failure);
        }

        Store.Log("Spotlight completely repaired and reset: " + backup);
        sb.AppendLine();
        sb.AppendLine($"پشتیبان فایل‌ها در مسیر زیر ذخیره شد:\n{backup}");
        sb.AppendLine("\nنکته نهایی: اکنون در Settings ویندوز > Personalization > Lock screen حالت را روی Windows Spotlight قرار دهید و سیستم را یک‌بار Lock (کلید Win+L) کنید.");
        return sb.ToString();
    }
    public static List<Photo> ImportSpotlight(string? archiveRoot = null)
    {
        archiveRoot ??= Store.Root;
        var assets = Path.Combine(SpotlightRoot, "LocalState", "Assets");
        if (!Directory.Exists(assets)) throw new InvalidOperationException("کش تصاویر Spotlight پیدا نشد.");
        var photos = new List<Photo>();
        Directory.CreateDirectory(archiveRoot);
        using var gate = Store.AcquireLockAsync(Path.Combine(archiveRoot, "feed.lock"), 10).GetAwaiter().GetResult();
        var destination = Path.Combine(archiveRoot, "Images"); Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(assets))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.Length < 100_000 || info.Length > 30_000_000) continue;
                var bitmap = Store.LoadImage(file, 0);
                if (bitmap.PixelWidth < 1000 || bitmap.PixelHeight < 600) continue;
                using var stream = File.OpenRead(file);
                var id = "spotlight-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))[..24];
                var path = Path.Combine(destination, id + ".jpg");
                if (!File.Exists(path) || !SourceCatalog.IsHealthy(new Photo { FilePath = path }))
                    SourceCatalog.ConvertToJpeg(file, path);
                photos.Add(new Photo { Id = id, Source = "Spotlight", Title = "Windows Spotlight", Copyright = "Microsoft Spotlight • cached locally; original attribution unavailable", FilePath = path, Date = info.LastWriteTime.ToString("yyyyMMdd"), Market = "Spotlight" });
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or FileFormatException) { Store.Log("Skipped Spotlight asset: " + ex.Message); }
        }
        var archivePath = Path.Combine(archiveRoot, "archive.json");
        Store.MergeArchive(archiveRoot, photos);
        return photos;
    }
    public static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    #region Win32 Global Hotkeys
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x0312;

    public const int HOTKEY_ID_NEXT_WALLPAPER = 0x9001;
    public const int HOTKEY_ID_FAVORITE = 0x9002;
    public const int HOTKEY_ID_TOGGLE_WIDGET = 0x9003;
    #endregion

    #region Win32 Multi-Monitor IDesktopWallpaper COM API
    public enum DesktopWallpaperPosition
    {
        Center = 0,
        Tile = 1,
        Stretch = 2,
        Fit = 3,
        Fill = 4,
        Span = 5
    }

    [ComImport]
    [Guid("B92B56A9-8555-4772-9338-78F20265BE04")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out System.Drawing.Rectangle displayRect);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(DesktopWallpaperPosition position);
        DesktopWallpaperPosition GetPosition();
        void SetSlideshow(IntPtr items);
        IntPtr GetSlideshow();
        void SetSlideshowOptions(uint options, uint slideshowTick);
        void GetSlideshowOptions(out uint options, out uint slideshowTick);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, uint direction);
        uint GetStatus();
        bool Enable();
    }

    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    public class DesktopWallpaperCoClass
    {
    }

    public static uint GetMonitorCount()
    {
        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperCoClass();
            return wallpaper.GetMonitorDevicePathCount();
        }
        catch
        {
            return 1;
        }
    }

    public static void SetMonitorWallpaper(uint monitorIndex, string filePath)
    {
        Store.LoadImage(filePath, 32);
        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperCoClass();
            var monitorId = wallpaper.GetMonitorDevicePathAt(monitorIndex);
            wallpaper.SetWallpaper(monitorId, Path.GetFullPath(filePath));
            Store.Log($"Multi-Monitor: Wallpaper set on display #{monitorIndex} ({monitorId}): {filePath}");
        }
        catch (Exception ex)
        {
            Store.Log($"Multi-Monitor COM fallback to standard SetDesktop: {ex.Message}");
            SetDesktop(filePath, "Fill");
        }
    }
    #endregion

    #region Theme Accent Color Sync
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    const uint WM_SETTINGCHANGE = 0x001A;
    const uint HWND_BROADCAST = 0xFFFF;
    const uint SMTO_ABORTIFHUNG = 0x0002;

    public static System.Windows.Media.Color CalculateDominantColor(string imagePath)
    {
        try
        {
            var bitmap = Store.LoadImage(imagePath, 64);
            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            if (width <= 0 || height <= 0) return System.Windows.Media.Color.FromRgb(18, 94, 83);

            var stride = (width * 32 + 7) / 8;
            var pixels = new byte[height * stride];
            var formatConverted = new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            formatConverted.CopyPixels(pixels, stride, 0);

            long totalR = 0, totalG = 0, totalB = 0;
            int count = 0;

            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    int index = y * stride + x * 4;
                    byte b = pixels[index];
                    byte g = pixels[index + 1];
                    byte r = pixels[index + 2];

                    // Exclude pure darks and washed out brights
                    int brightness = (r + g + b) / 3;
                    if (brightness is > 30 and < 230)
                    {
                        totalR += r;
                        totalG += g;
                        totalB += b;
                        count++;
                    }
                }
            }

            if (count == 0) return System.Windows.Media.Color.FromRgb(20, 102, 87);
            return System.Windows.Media.Color.FromRgb((byte)(totalR / count), (byte)(totalG / count), (byte)(totalB / count));
        }
        catch
        {
            return System.Windows.Media.Color.FromRgb(18, 94, 83);
        }
    }

    public static void SyncWindowsAccentColor(string imagePath)
    {
        try
        {
            var color = CalculateDominantColor(imagePath);
            int argb = (int)((0xFFu << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B);

            try
            {
                using var dwmKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\DWM");
                if (dwmKey != null)
                {
                    dwmKey.SetValue("ColorizationColor", argb, RegistryValueKind.DWord);
                    dwmKey.SetValue("AccentColor", argb, RegistryValueKind.DWord);
                }

                using var accentKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
                if (accentKey != null)
                {
                    accentKey.SetValue("AccentColorMenu", argb, RegistryValueKind.DWord);
                }

                SendMessageTimeout(new IntPtr(HWND_BROADCAST), WM_SETTINGCHANGE, UIntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 1000, out _);
            }
            catch { }

            Store.Log($"Windows Theme Accent Color synced to dominant color #{color.R:X2}{color.G:X2}{color.B:X2}");
        }
        catch (Exception ex)
        {
            Store.Log("Accent color sync warning: " + ex.Message);
        }
    }
    #endregion

    #region Desktop Icons Clean Mode
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static void ToggleDesktopIcons(bool? forceShow = null)
    {
        try
        {
            var progman = FindWindow("Progman", null);
            var shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero)
            {
                var workerW = IntPtr.Zero;
                do
                {
                    workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
                    shellView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                } while (workerW != IntPtr.Zero && shellView == IntPtr.Zero);
            }

            if (shellView != IntPtr.Zero)
            {
                // Send Toggle Desktop Icons command (0x7402)
                SendMessage(shellView, 0x0111, new IntPtr(0x7402), IntPtr.Zero);
                Store.Log("Desktop icons visibility toggled.");
            }
        }
        catch (Exception ex)
        {
            Store.Log("ToggleDesktopIcons error: " + ex.Message);
        }
    }
    #endregion
}
