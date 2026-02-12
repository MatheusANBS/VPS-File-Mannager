using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VPSFileManager.Services
{
    /// <summary>
    /// Collects VPS system metrics by executing lightweight Linux commands over SSH.
    /// Strategy inspired by btop/glances: read /proc pseudo-files for zero-overhead sampling.
    /// </summary>
    public class DashboardService
    {
        private readonly ISftpService _sftpService;

        // Previous CPU sample for delta calculation
        private long _prevCpuUser, _prevCpuNice, _prevCpuSystem, _prevCpuIdle;
        private long _prevCpuIowait, _prevCpuIrq, _prevCpuSoftirq, _prevCpuSteal;
        private bool _hasPreviousCpuSample;

        // Previous network sample for rate calculation
        private Dictionary<string, (long rx, long tx)> _prevNetSample = new();
        private DateTime _prevNetTime = DateTime.MinValue;

        public DashboardService(ISftpService sftpService)
        {
            _sftpService = sftpService;
        }

        /// <summary>
        /// Collects all system metrics in a single SSH command batch.
        /// </summary>
        public async Task<SystemMetrics> CollectAsync(CancellationToken ct = default)
        {
            // Single SSH round-trip: use delimiters to split output
            const string separator = "===VPSMETRIC===";
            var batchCommand =
                $"cat /proc/stat | head -1; echo '{separator}';" +
                $"cat /proc/meminfo; echo '{separator}';" +
                $"df -P -B1 --total 2>/dev/null || df -P -k --total 2>/dev/null; echo '{separator}';" +
                $"cat /proc/net/dev; echo '{separator}';" +
                $"cat /proc/loadavg; echo '{separator}';" +
                $"uptime -s 2>/dev/null || uptime; echo '{separator}';" +
                $"nproc 2>/dev/null || grep -c '^processor' /proc/cpuinfo; echo '{separator}';" +
                $"cat /proc/version 2>/dev/null || uname -r; echo '{separator}';" +
                $"hostname; echo '{separator}';" +
                $"ps aux --sort=-%cpu 2>/dev/null | head -11; echo '{separator}';" +
                $"ps aux --sort=-%mem 2>/dev/null | head -11; echo '{separator}';" +
                $"cat /etc/os-release 2>/dev/null | head -5; echo '{separator}';" +
                $"ip -4 addr show 2>/dev/null | grep -E 'inet |^[0-9]+:' || hostname -I 2>/dev/null";

            ct.ThrowIfCancellationRequested();
            var rawOutput = await _sftpService.ExecuteCommandAsync(batchCommand);
            ct.ThrowIfCancellationRequested();

            var sections = rawOutput.Split(new[] { separator }, StringSplitOptions.None);

            var metrics = new SystemMetrics
            {
                CollectedAt = DateTime.Now
            };

            if (sections.Length >= 1) ParseCpu(sections[0].Trim(), metrics);
            if (sections.Length >= 2) ParseMemory(sections[1].Trim(), metrics);
            if (sections.Length >= 3) ParseDisk(sections[2].Trim(), metrics);
            if (sections.Length >= 4) ParseNetwork(sections[3].Trim(), metrics);
            if (sections.Length >= 5) ParseLoadAvg(sections[4].Trim(), metrics);
            if (sections.Length >= 6) ParseUptime(sections[5].Trim(), metrics);
            if (sections.Length >= 7) ParseCpuCores(sections[6].Trim(), metrics);
            if (sections.Length >= 8) metrics.KernelVersion = sections[7].Trim().Split('\n')[0];
            if (sections.Length >= 9) metrics.Hostname = sections[8].Trim();
            if (sections.Length >= 10) ParseProcesses(sections[9].Trim(), metrics.TopProcesses);
            if (sections.Length >= 11) ParseProcesses(sections[10].Trim(), metrics.TopProcessesByMem);
            if (sections.Length >= 12) ParseOsRelease(sections[11].Trim(), metrics);
            if (sections.Length >= 13) ParseIpAddresses(sections[12].Trim(), metrics);

            return metrics;
        }

        private void ParseIpAddresses(string data, SystemMetrics m)
        {
            // Parse "ip -4 addr show" output to extract interface -> IP mapping
            // Format: "2: eth0: ...\n    inet 192.168.1.10/24 ..."
            var lines = data.Split('\n');
            string? currentIface = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Interface line: "2: eth0: <...>"
                if (!trimmed.StartsWith("inet") && trimmed.Contains(":"))
                {
                    var colonIdx = trimmed.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var afterDigit = trimmed.Substring(colonIdx + 1).Trim();
                        var nextColon = afterDigit.IndexOf(':');
                        if (nextColon > 0)
                            currentIface = afterDigit.Substring(0, nextColon).Trim();
                    }
                }
                // IP line: "inet 192.168.1.10/24 ..."
                else if (trimmed.StartsWith("inet ") && currentIface != null)
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var ipWithMask = parts[1];
                        var ip = ipWithMask.Split('/')[0];

                        // Find matching network interface and set IP
                        var netIface = m.NetworkInterfaces.Find(n => n.Name == currentIface);
                        if (netIface != null && string.IsNullOrEmpty(netIface.IpAddress))
                        {
                            netIface.IpAddress = ip;
                        }
                    }
                }
            }
        }

        private void ParseCpu(string data, SystemMetrics m)
        {
            // "cpu  user nice system idle iowait irq softirq steal guest guest_nice"
            var parts = data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 || parts[0] != "cpu") return;

            long user = ParseLong(parts, 1);
            long nice = ParseLong(parts, 2);
            long system = ParseLong(parts, 3);
            long idle = ParseLong(parts, 4);
            long iowait = ParseLong(parts, 5);
            long irq = ParseLong(parts, 6);
            long softirq = ParseLong(parts, 7);
            long steal = ParseLong(parts, 8);

            if (_hasPreviousCpuSample)
            {
                long dUser = user - _prevCpuUser;
                long dNice = nice - _prevCpuNice;
                long dSystem = system - _prevCpuSystem;
                long dIdle = idle - _prevCpuIdle;
                long dIowait = iowait - _prevCpuIowait;
                long dIrq = irq - _prevCpuIrq;
                long dSoftirq = softirq - _prevCpuSoftirq;
                long dSteal = steal - _prevCpuSteal;

                long total = dUser + dNice + dSystem + dIdle + dIowait + dIrq + dSoftirq + dSteal;
                if (total > 0)
                {
                    m.CpuUsagePercent = Math.Round(100.0 * (total - dIdle - dIowait) / total, 1);
                    m.CpuUserPercent = Math.Round(100.0 * dUser / total, 1);
                    m.CpuSystemPercent = Math.Round(100.0 * dSystem / total, 1);
                    m.CpuIoWaitPercent = Math.Round(100.0 * dIowait / total, 1);
                    m.CpuStealPercent = Math.Round(100.0 * dSteal / total, 1);
                }
            }

            _prevCpuUser = user; _prevCpuNice = nice;
            _prevCpuSystem = system; _prevCpuIdle = idle;
            _prevCpuIowait = iowait; _prevCpuIrq = irq;
            _prevCpuSoftirq = softirq; _prevCpuSteal = steal;
            _hasPreviousCpuSample = true;
        }

        private void ParseMemory(string data, SystemMetrics m)
        {
            var lines = data.Split('\n');
            var memDict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out long val))
                {
                    memDict[parts[0]] = val; // in kB
                }
            }

            m.MemTotalKB = memDict.GetValueOrDefault("MemTotal");
            m.MemFreeKB = memDict.GetValueOrDefault("MemFree");
            m.MemAvailableKB = memDict.GetValueOrDefault("MemAvailable");
            m.MemBuffersKB = memDict.GetValueOrDefault("Buffers");
            m.MemCachedKB = memDict.GetValueOrDefault("Cached");
            m.SwapTotalKB = memDict.GetValueOrDefault("SwapTotal");
            m.SwapFreeKB = memDict.GetValueOrDefault("SwapFree");

            long used = m.MemTotalKB - m.MemAvailableKB;
            if (m.MemTotalKB > 0)
                m.MemUsagePercent = Math.Round(100.0 * used / m.MemTotalKB, 1);

            if (m.SwapTotalKB > 0)
                m.SwapUsagePercent = Math.Round(100.0 * (m.SwapTotalKB - m.SwapFreeKB) / m.SwapTotalKB, 1);
        }

        private void ParseDisk(string data, SystemMetrics m)
        {
            var lines = data.Split('\n');
            m.Disks = new List<DiskInfo>();

            foreach (var line in lines)
            {
                if (line.StartsWith("Filesystem") || string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6) continue;

                // Skip pseudo-filesystems
                if (parts[0].StartsWith("tmpfs") || parts[0].StartsWith("devtmpfs") ||
                    parts[0].StartsWith("udev") || parts[0] == "none" ||
                    parts[5].StartsWith("/snap") || parts[5].StartsWith("/boot/efi")) continue;

                if (!long.TryParse(parts[1], out long total)) continue;

                var disk = new DiskInfo
                {
                    Filesystem = parts[0],
                    MountPoint = parts[5],
                    TotalBytes = total,
                    UsedBytes = long.TryParse(parts[2], out long u) ? u : 0,
                    AvailableBytes = long.TryParse(parts[3], out long a) ? a : 0
                };

                if (disk.TotalBytes > 0)
                    disk.UsagePercent = Math.Round(100.0 * disk.UsedBytes / disk.TotalBytes, 1);

                // If df gave kB output (no -B1 support), convert
                if (disk.TotalBytes < 1_000_000_000 && disk.TotalBytes > 100)
                {
                    disk.TotalBytes *= 1024;
                    disk.UsedBytes *= 1024;
                    disk.AvailableBytes *= 1024;
                }

                m.Disks.Add(disk);
            }
        }

        private void ParseNetwork(string data, SystemMetrics m)
        {
            var lines = data.Split('\n');
            var currentSample = new Dictionary<string, (long rx, long tx)>();
            var now = DateTime.Now;
            m.NetworkInterfaces = new List<NetworkInterfaceInfo>();

            foreach (var line in lines)
            {
                if (!line.Contains(':') || line.TrimStart().StartsWith("Inter") || line.TrimStart().StartsWith("face")) continue;

                var colonIdx = line.IndexOf(':');
                var iface = line.Substring(0, colonIdx).Trim();
                var rest = line.Substring(colonIdx + 1).Trim();
                var parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 10) continue;
                if (iface == "lo") continue;

                long rxBytes = long.TryParse(parts[0], out long r) ? r : 0;
                long txBytes = long.TryParse(parts[8], out long t) ? t : 0;

                currentSample[iface] = (rxBytes, txBytes);

                var info = new NetworkInterfaceInfo
                {
                    Name = iface,
                    RxTotalBytes = rxBytes,
                    TxTotalBytes = txBytes
                };

                // Calculate rates from previous sample
                if (_prevNetSample.ContainsKey(iface) && _prevNetTime > DateTime.MinValue)
                {
                    var elapsed = (now - _prevNetTime).TotalSeconds;
                    if (elapsed > 0)
                    {
                        var prev = _prevNetSample[iface];
                        info.RxBytesPerSec = (long)((rxBytes - prev.rx) / elapsed);
                        info.TxBytesPerSec = (long)((txBytes - prev.tx) / elapsed);
                    }
                }

                m.NetworkInterfaces.Add(info);
            }

            _prevNetSample = currentSample;
            _prevNetTime = now;
        }

        private void ParseLoadAvg(string data, SystemMetrics m)
        {
            var parts = data.Split(' ');
            if (parts.Length >= 3)
            {
                double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double l1);
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double l5);
                double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double l15);
                m.LoadAvg1 = l1;
                m.LoadAvg5 = l5;
                m.LoadAvg15 = l15;
            }
        }

        private void ParseUptime(string data, SystemMetrics m)
        {
            // "uptime -s" returns "2024-01-15 10:30:00"
            if (DateTime.TryParse(data.Trim(), out DateTime bootTime))
            {
                m.Uptime = DateTime.Now - bootTime;
            }
            else
            {
                // Fallback: parse from "uptime" output "up X days, HH:MM"
                var upIdx = data.IndexOf("up ");
                if (upIdx >= 0)
                    m.UptimeRaw = data.Substring(upIdx + 3).Split(',')[0].Trim();
            }
        }

        private void ParseCpuCores(string data, SystemMetrics m)
        {
            if (int.TryParse(data.Trim(), out int cores))
                m.CpuCores = cores;
        }

        private void ParseProcesses(string data, List<ProcessInfo> targetList)
        {
            var lines = data.Split('\n');
            targetList.Clear();

            foreach (var line in lines)
            {
                if (line.StartsWith("USER") || string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 11) continue;

                var proc = new ProcessInfo
                {
                    User = parts[0],
                    Pid = parts[1],
                    CpuPercent = double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double c) ? c : 0,
                    MemPercent = double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double mem) ? mem : 0,
                    Vsz = parts[4],
                    Rss = parts[5],
                    Stat = parts[7],
                    Command = string.Join(" ", parts.Skip(10))
                };

                targetList.Add(proc);
            }
        }

        private void ParseOsRelease(string data, SystemMetrics m)
        {
            foreach (var line in data.Split('\n'))
            {
                if (line.StartsWith("PRETTY_NAME="))
                {
                    m.OsName = line.Substring(12).Trim('"', '\'', ' ');
                    return;
                }
            }
            // Fallback
            foreach (var line in data.Split('\n'))
            {
                if (line.StartsWith("NAME="))
                {
                    m.OsName = line.Substring(5).Trim('"', '\'', ' ');
                    return;
                }
            }
        }

        private static long ParseLong(string[] parts, int idx)
        {
            if (idx < parts.Length && long.TryParse(parts[idx], out long val)) return val;
            return 0;
        }
    }

    // ────── Data models ──────

    public class SystemMetrics
    {
        public DateTime CollectedAt { get; set; }
        public string Hostname { get; set; } = "";
        public string OsName { get; set; } = "";
        public string KernelVersion { get; set; } = "";
        public int CpuCores { get; set; }

        // CPU
        public double CpuUsagePercent { get; set; }
        public double CpuUserPercent { get; set; }
        public double CpuSystemPercent { get; set; }
        public double CpuIoWaitPercent { get; set; }
        public double CpuStealPercent { get; set; }

        // Memory (kB)
        public long MemTotalKB { get; set; }
        public long MemFreeKB { get; set; }
        public long MemAvailableKB { get; set; }
        public long MemBuffersKB { get; set; }
        public long MemCachedKB { get; set; }
        public long MemUsedKB => MemTotalKB - MemAvailableKB;
        public double MemUsagePercent { get; set; }

        // Swap
        public long SwapTotalKB { get; set; }
        public long SwapFreeKB { get; set; }
        public long SwapUsedKB => SwapTotalKB - SwapFreeKB;
        public double SwapUsagePercent { get; set; }

        // Load
        public double LoadAvg1 { get; set; }
        public double LoadAvg5 { get; set; }
        public double LoadAvg15 { get; set; }

        // Uptime
        public TimeSpan? Uptime { get; set; }
        public string? UptimeRaw { get; set; }

        // Disk
        public List<DiskInfo> Disks { get; set; } = new();

        // Network
        public List<NetworkInterfaceInfo> NetworkInterfaces { get; set; } = new();

        // Processes
        public List<ProcessInfo> TopProcesses { get; set; } = new();
        public List<ProcessInfo> TopProcessesByMem { get; set; } = new();
    }

    public class DiskInfo
    {
        public string Filesystem { get; set; } = "";
        public string MountPoint { get; set; } = "";
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long AvailableBytes { get; set; }
        public double UsagePercent { get; set; }
    }

    public class NetworkInterfaceInfo
    {
        public string Name { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public long RxTotalBytes { get; set; }
        public long TxTotalBytes { get; set; }
        public long RxBytesPerSec { get; set; }
        public long TxBytesPerSec { get; set; }
    }

    public class ProcessInfo
    {
        public string User { get; set; } = "";
        public string Pid { get; set; } = "";
        public double CpuPercent { get; set; }
        public double MemPercent { get; set; }
        public string Vsz { get; set; } = "";
        public string Rss { get; set; } = "";
        public string Stat { get; set; } = "";
        public string Command { get; set; } = "";
    }
}
