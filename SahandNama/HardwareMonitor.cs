using System.Runtime.InteropServices;

namespace SahandNama;

public sealed class HardwareInfo
{
    public double CpuPercent { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public int RamPercent { get; set; }
    public bool HasBattery { get; set; }
    public int BatteryPercent { get; set; }
    public bool IsCharging { get; set; }

    public string CpuSummary => $"CPU: {Math.Round(CpuPercent)}%";
    public string RamSummary => $"RAM: {RamPercent}% ({RamUsedGb:F1}/{RamTotalGb:F1} GB)";
    public string BatterySummary => HasBattery ? (IsCharging ? $"⚡ {BatteryPercent}%" : $"🔋 {BatteryPercent}%") : "";
    public string FormattedBadge => HasBattery 
        ? $"{CpuSummary}  |  {RamSummary}  |  {BatterySummary}" 
        : $"{CpuSummary}  |  {RamSummary}";
}

public static class HardwareMonitor
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

    static long prevIdleTime;
    static long prevKernelTime;
    static long prevUserTime;
    static double lastCpuPercent;
    static DateTime lastCpuCheck = DateTime.MinValue;

    public static HardwareInfo GetCurrentStatus()
    {
        var info = new HardwareInfo();

        // 1. RAM Usage
        try
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                var totalGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                var availGb = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                info.RamTotalGb = totalGb;
                info.RamUsedGb = Math.Max(0, totalGb - availGb);
                info.RamPercent = (int)memStatus.dwMemoryLoad;
            }
        }
        catch (Exception ex)
        {
            Store.Log("HardwareMonitor RAM error: " + ex.Message);
        }

        // 2. CPU Usage
        try
        {
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                if (prevKernelTime > 0 && (DateTime.UtcNow - lastCpuCheck).TotalMilliseconds > 400)
                {
                    var idleDelta = idle - prevIdleTime;
                    var kernelDelta = kernel - prevKernelTime;
                    var userDelta = user - prevUserTime;
                    var totalDelta = kernelDelta + userDelta;

                    if (totalDelta > 0)
                    {
                        var cpu = (1.0 - ((double)idleDelta / totalDelta)) * 100.0;
                        lastCpuPercent = Math.Clamp(cpu, 0.0, 100.0);
                    }
                    lastCpuCheck = DateTime.UtcNow;
                }

                prevIdleTime = idle;
                prevKernelTime = kernel;
                prevUserTime = user;
                info.CpuPercent = lastCpuPercent;
            }
        }
        catch (Exception ex)
        {
            Store.Log("HardwareMonitor CPU error: " + ex.Message);
        }

        // 3. Battery / Power Status
        try
        {
            if (GetSystemPowerStatus(out var powerStatus))
            {
                if (powerStatus.BatteryLifePercent != 255 && powerStatus.BatteryFlag != 128)
                {
                    info.HasBattery = true;
                    info.BatteryPercent = Math.Clamp((int)powerStatus.BatteryLifePercent, 0, 100);
                    info.IsCharging = powerStatus.ACLineStatus == 1;
                }
            }
        }
        catch (Exception ex)
        {
            Store.Log("HardwareMonitor Battery error: " + ex.Message);
        }

        return info;
    }
}
