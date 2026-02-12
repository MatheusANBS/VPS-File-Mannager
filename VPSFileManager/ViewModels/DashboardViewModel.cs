using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPSFileManager.Services;

namespace VPSFileManager.ViewModels
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly DashboardService _dashboardService;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        // ── System info ──
        [ObservableProperty] private string _hostname = "—";
        [ObservableProperty] private string _osName = "—";
        [ObservableProperty] private string _kernelVersion = "—";
        [ObservableProperty] private string _uptimeDisplay = "—";
        [ObservableProperty] private int _cpuCores;

        // ── CPU ──
        [ObservableProperty] private double _cpuUsage;
        [ObservableProperty] private string _cpuDetail = "";
        [ObservableProperty] private string _loadAverage = "";

        // ── Memory ──
        [ObservableProperty] private double _memUsage;
        [ObservableProperty] private string _memDetail = "";
        [ObservableProperty] private string _memUsedDisplay = "";
        [ObservableProperty] private string _memTotalDisplay = "";
        [ObservableProperty] private string _memFreeDisplay = "";

        // ── Swap ──
        [ObservableProperty] private double _swapUsage;
        [ObservableProperty] private string _swapDetail = "";
        [ObservableProperty] private bool _hasSwap;

        // ── Disk ──
        [ObservableProperty] private ObservableCollection<DiskDisplayInfo> _disks = new();

        // ── Primary disk (for donut chart) ──
        [ObservableProperty] private double _primaryDiskUsage;
        [ObservableProperty] private string _primaryDiskUsed = "";
        [ObservableProperty] private string _primaryDiskTotal = "";
        [ObservableProperty] private string _primaryDiskAvailable = "";
        [ObservableProperty] private string _primaryDiskMount = "";

        // ── Network ──
        [ObservableProperty] private ObservableCollection<NetworkDisplayInfo> _networkInterfaces = new();

        // ── Processes ──
        [ObservableProperty] private ObservableCollection<ProcessDisplayInfo> _processes = new();
        [ObservableProperty] private ObservableCollection<ProcessDisplayInfo> _processesByMem = new();

        // ── State ──
        [ObservableProperty] private bool _isLoading = true;
        [ObservableProperty] private bool _isRefreshing;
        [ObservableProperty] private string _lastUpdated = "";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private int _refreshIntervalSeconds = 3;
        [ObservableProperty] private bool _autoRefresh = true;

        // CPU history for sparkline (last 60 samples)
        [ObservableProperty] private ObservableCollection<double> _cpuHistory = new();
        [ObservableProperty] private ObservableCollection<double> _memHistory = new();

        public DashboardViewModel(ISftpService sftpService)
        {
            _dashboardService = new DashboardService(sftpService);
        }

        public async Task StartMonitoringAsync()
        {
            _cts = new CancellationTokenSource();
            IsLoading = true;

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    await RefreshMetricsAsync();

                    if (!AutoRefresh)
                    {
                        IsLoading = false;
                        return;
                    }

                    await Task.Delay(RefreshIntervalSeconds * 1000, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                HasError = true;
            }
        }

        [RelayCommand]
        private async Task RefreshMetricsAsync()
        {
            try
            {
                IsRefreshing = true;
                HasError = false;

                var m = await _dashboardService.CollectAsync(_cts?.Token ?? CancellationToken.None);

                // System info
                Hostname = m.Hostname;
                OsName = m.OsName;
                KernelVersion = m.KernelVersion;
                CpuCores = m.CpuCores;

                // Uptime
                if (m.Uptime.HasValue)
                {
                    var u = m.Uptime.Value;
                    UptimeDisplay = u.TotalDays >= 1
                        ? $"{(int)u.TotalDays}d {u.Hours}h {u.Minutes}m"
                        : $"{u.Hours}h {u.Minutes}m {u.Seconds}s";
                }
                else if (!string.IsNullOrEmpty(m.UptimeRaw))
                {
                    UptimeDisplay = m.UptimeRaw;
                }

                // CPU
                CpuUsage = m.CpuUsagePercent;
                CpuDetail = $"usr {m.CpuUserPercent}%  sys {m.CpuSystemPercent}%  io {m.CpuIoWaitPercent}%  steal {m.CpuStealPercent}%";
                LoadAverage = $"{m.LoadAvg1:F2}  {m.LoadAvg5:F2}  {m.LoadAvg15:F2}";

                // CPU history
                CpuHistory.Add(m.CpuUsagePercent);
                if (CpuHistory.Count > 60) CpuHistory.RemoveAt(0);

                // Memory
                MemUsage = m.MemUsagePercent;
                MemUsedDisplay = FormatBytes(m.MemUsedKB * 1024);
                MemTotalDisplay = FormatBytes(m.MemTotalKB * 1024);
                MemFreeDisplay = FormatBytes(m.MemAvailableKB * 1024);
                MemDetail = $"Used {FormatBytes(m.MemUsedKB * 1024)} / {FormatBytes(m.MemTotalKB * 1024)}  •  Buffers {FormatBytes(m.MemBuffersKB * 1024)}  Cache {FormatBytes(m.MemCachedKB * 1024)}";

                MemHistory.Add(m.MemUsagePercent);
                if (MemHistory.Count > 60) MemHistory.RemoveAt(0);

                // Swap
                HasSwap = m.SwapTotalKB > 0;
                SwapUsage = m.SwapUsagePercent;
                SwapDetail = HasSwap
                    ? $"Used {FormatBytes(m.SwapUsedKB * 1024)} / {FormatBytes(m.SwapTotalKB * 1024)}"
                    : "No swap";

                // Disks
                var diskList = new ObservableCollection<DiskDisplayInfo>();
                foreach (var d in m.Disks)
                {
                    diskList.Add(new DiskDisplayInfo
                    {
                        MountPoint = d.MountPoint,
                        Filesystem = d.Filesystem,
                        UsedDisplay = FormatBytes(d.UsedBytes),
                        TotalDisplay = FormatBytes(d.TotalBytes),
                        AvailableDisplay = FormatBytes(d.AvailableBytes),
                        UsagePercent = d.UsagePercent
                    });
                }
                Disks = diskList;

                // Primary disk (/ or largest)
                if (m.Disks.Count > 0)
                {
                    var primaryDisk = m.Disks.Find(d => d.MountPoint == "/") ?? m.Disks[0];
                    PrimaryDiskUsage = primaryDisk.UsagePercent;
                    PrimaryDiskUsed = FormatBytes(primaryDisk.UsedBytes);
                    PrimaryDiskTotal = FormatBytes(primaryDisk.TotalBytes);
                    PrimaryDiskAvailable = FormatBytes(primaryDisk.AvailableBytes);
                    PrimaryDiskMount = primaryDisk.MountPoint;
                }

                // Network
                var netList = new ObservableCollection<NetworkDisplayInfo>();
                foreach (var n in m.NetworkInterfaces)
                {
                    netList.Add(new NetworkDisplayInfo
                    {
                        Name = n.Name,
                        IpAddress = string.IsNullOrEmpty(n.IpAddress) ? "—" : n.IpAddress,
                        RxRate = FormatRate(n.RxBytesPerSec),
                        TxRate = FormatRate(n.TxBytesPerSec),
                        RxTotal = FormatBytes(n.RxTotalBytes),
                        TxTotal = FormatBytes(n.TxTotalBytes)
                    });
                }
                NetworkInterfaces = netList;

                // Processes by CPU
                var procList = new ObservableCollection<ProcessDisplayInfo>();
                foreach (var p in m.TopProcesses)
                {
                    procList.Add(new ProcessDisplayInfo
                    {
                        Pid = p.Pid,
                        User = p.User,
                        CpuPercent = p.CpuPercent,
                        MemPercent = p.MemPercent,
                        Command = p.Command,
                        Status = p.Stat
                    });
                }
                Processes = procList;

                // Processes by Memory
                var procByMemList = new ObservableCollection<ProcessDisplayInfo>();
                foreach (var p in m.TopProcessesByMem)
                {
                    procByMemList.Add(new ProcessDisplayInfo
                    {
                        Pid = p.Pid,
                        User = p.User,
                        CpuPercent = p.CpuPercent,
                        MemPercent = p.MemPercent,
                        Command = p.Command,
                        Status = p.Stat
                    });
                }
                ProcessesByMem = procByMemList;

                LastUpdated = m.CollectedAt.ToString("HH:mm:ss");
                IsLoading = false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                HasError = true;
                IsLoading = false;
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        public void StopMonitoring()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ── Helpers ──

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double d = bytes;
            while (d >= 1024 && order < sizes.Length - 1)
            {
                order++;
                d /= 1024;
            }
            return $"{d:0.#} {sizes[order]}";
        }

        public static string FormatRate(long bytesPerSec)
        {
            if (bytesPerSec <= 0) return "0 B/s";
            string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s" };
            int order = 0;
            double d = bytesPerSec;
            while (d >= 1024 && order < sizes.Length - 1)
            {
                order++;
                d /= 1024;
            }
            return $"{d:0.#} {sizes[order]}";
        }
    }

    // ── Display models for binding ──

    public class DiskDisplayInfo
    {
        public string MountPoint { get; set; } = "";
        public string Filesystem { get; set; } = "";
        public string UsedDisplay { get; set; } = "";
        public string TotalDisplay { get; set; } = "";
        public string AvailableDisplay { get; set; } = "";
        public double UsagePercent { get; set; }
    }

    public class NetworkDisplayInfo
    {
        public string Name { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string RxRate { get; set; } = "";
        public string TxRate { get; set; } = "";
        public string RxTotal { get; set; } = "";
        public string TxTotal { get; set; } = "";
    }

    public class ProcessDisplayInfo
    {
        public string Pid { get; set; } = "";
        public string User { get; set; } = "";
        public double CpuPercent { get; set; }
        public double MemPercent { get; set; }
        public string Command { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
