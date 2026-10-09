using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using App.Components;
using App.DAL;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages
{
    public class AboutModel : AdminModel
    {
        [BindProperty] public string SiteTitle { get; set; }
        [BindProperty] public string ProductVersion { get; set; }
        [BindProperty] public string InformationalVersion { get; set; }
        [BindProperty] public string BuildTime { get; set; }
        [BindProperty] public string NetVersion { get; set; }
        [BindProperty] public string RuntimeVersion { get; set; }
        [BindProperty] public string OperatingSystem { get; set; }
        [BindProperty] public string ProcessArchitecture { get; set; }
        [BindProperty] public string AppStartedAt { get; set; }
        [BindProperty] public string StartupDurationMs { get; set; }
        [BindProperty] public string UptimeText { get; set; }
        [BindProperty] public string CpuUsageText { get; set; }
        [BindProperty] public string MemoryUsedText { get; set; }
        [BindProperty] public string DiskUsedText { get; set; }
        [BindProperty] public string Author { get; set; } = "surfsky";
        [BindProperty]public string ProjectUrl { get; set; } = "https://github.com/surfsky/AppPlat8";
        [BindProperty] public string License { get; set; } = "MIT";

        public void OnGet()
        {
            SetSiteTitleAndVersion();
            LoadAssemblyInfo();
            LoadRuntimeEnvironment();
            LoadStartupTime();
            SampleCpuUsage();
            SampleMemoryUsage();
            SampleDiskUsage();
        }

        //--------------------------------------
        // 基础信息：站点标题 / 版本号
        //--------------------------------------
        /// <summary>填充站点标题、产品版本号两个基础字段</summary>
        private void SetSiteTitleAndVersion()
        {
            SiteTitle = SiteConfig.Instance.Title;
            ProductVersion = Common.GetVersion();
        }

        //--------------------------------------
        // 程序集信息：InformationalVersion / BuildTime
        //--------------------------------------
        /// <summary>从入口程序集读取信息版本与构建时间</summary>
        private void LoadAssemblyInfo()
        {
            try
            {
                var asm = Assembly.GetEntryAssembly();
                if (asm != null)
                {
                    var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (infoVer != null)
                        InformationalVersion = infoVer.InformationalVersion?.Trim();
                    if (InformationalVersion.IsEmpty())
                        InformationalVersion = asm.GetName().Version?.ToString() ?? ProductVersion;
                    try
                    {
                        var fi = new System.IO.FileInfo(asm.Location);
                        if (fi.Exists)
                            BuildTime = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    catch
                    {
                        BuildTime = "-";
                    }
                }
                else
                {
                    InformationalVersion = ProductVersion;
                    BuildTime = "-";
                }
            }
            catch
            {
                InformationalVersion = ProductVersion;
                BuildTime = "-";
            }
        }

        //--------------------------------------
        // 运行环境：.NET 版本 / 运行时描述 / OS / 架构
        //--------------------------------------
        /// <summary>填充 .NET / 运行时 / 操作系统 / 进程架构四项运行环境</summary>
        private void LoadRuntimeEnvironment()
        {
            try
            {
                NetVersion = Environment.Version.ToString();
            }
            catch
            {
                NetVersion = "-";
            }
            try
            {
                RuntimeVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription?.Trim()
                    ?? (".NET " + NetVersion);
            }
            catch
            {
                RuntimeVersion = ".NET " + NetVersion;
            }
            try
            {
                OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription?.Trim() ?? Environment.OSVersion.ToString();
            }
            catch
            {
                OperatingSystem = Environment.OSVersion.ToString();
            }
            try
            {
                ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
            }
            catch
            {
                ProcessArchitecture = "-";
            }
        }

        //--------------------------------------
        // 启动时间与已运行时长：保障 StartupDt 不为空并计算
        //--------------------------------------
        /// <summary>取启动时间（优先 DB），必要时回填 DB，并计算「已运行」时分秒</summary>
        private void LoadStartupTime()
        {
            DateTime? startupDt = SiteConfig.Instance?.StartupDt;
            if (!startupDt.HasValue)
                startupDt = GetProcessStartDt();

            AppStartedAt = startupDt.Value.ToString("yyyy-MM-dd HH:mm:ss");
            var dur = DateTime.Now - startupDt.Value;
            if (dur.TotalMilliseconds > 0 && dur.TotalDays < 365 * 10)
            {
                StartupDurationMs = ((long)dur.TotalMilliseconds).ToString("N0") + "  ms";
                UptimeText = dur.Days > 0
                    ? $"{dur.Days}天 {dur.Hours:00}:{dur.Minutes:00}:{dur.Seconds:00}"
                    : $"{dur.Hours:00}:{dur.Minutes:00}:{dur.Seconds:00}";
            }
            else
            {
                StartupDurationMs = "-";
                UptimeText = "-";
            }
        }

        /// <summary>获取当前进程启动时间</summary>
        private static DateTime GetProcessStartDt()
        {
            try
            {
                using var p = System.Diagnostics.Process.GetCurrentProcess();
                if (p.StartTime > new DateTime(2000, 1, 1))
                    return p.StartTime;
            }
            catch
            {
                // ignore
            }
            return DateTime.Now;
        }


        //--------------------------------------
        // 资源采样：CPU / 内存 / 磁盘（三个方法按调用顺序放一起）
        //--------------------------------------
        /// <summary>跨平台两次采样 TotalProcessorTime 计算当前进程 CPU 占用率</summary>
        private void SampleCpuUsage()
        {
            try
            {
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                var t1 = DateTime.UtcNow;
                var cpu1 = proc.TotalProcessorTime;
                System.Threading.Thread.SpinWait(120000); // ≈ 15~30ms 轻量采样，不阻塞主线程太久
                proc.Refresh();
                var t2 = DateTime.UtcNow;
                var cpu2 = proc.TotalProcessorTime;
                var cpuUsedMs = (cpu2 - cpu1).TotalMilliseconds;
                var totalMs = (t2 - t1).TotalMilliseconds * Environment.ProcessorCount;
                var pct = totalMs > 0 ? Math.Max(0, Math.Min(100, cpuUsedMs * 100 / totalMs)) : 0;
                CpuUsageText = $"{pct:F1} %";
            }
            catch
            {
                CpuUsageText = "-";
            }
        }

        /// <summary>采样进程工作集与 GC 可见可用内存，形成「使用 / 总量」字符串</summary>
        private void SampleMemoryUsage()
        {
            try
            {
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                proc.Refresh();
                var used = proc.WorkingSet64;
                double usedGB = used / (1024.0 * 1024 * 1024);
                try
                {
                    var gcmem = GC.GetGCMemoryInfo();
                    double totalGB = gcmem.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
                    MemoryUsedText = $"{usedGB:F2} / {totalGB:F1} GB";
                }
                catch
                {
                    MemoryUsedText = $"{usedGB:F2} GB";
                }
            }
            catch
            {
                MemoryUsedText = "-";
            }
        }

        /// <summary>对应用所在盘符采样「磁盘使用 / 总量」</summary>
        private void SampleDiskUsage()
        {
            try
            {
                var di = new DriveInfo(Environment.CurrentDirectory);
                if (di.IsReady)
                {
                    var totalGB = di.TotalSize / (1024.0 * 1024 * 1024);
                    var usedGB = (di.TotalSize - di.AvailableFreeSpace) / (1024.0 * 1024 * 1024);
                    DiskUsedText = $"{usedGB:F1} / {totalGB:F0} GB";
                }
                else
                {
                    DiskUsedText = "-";
                }
            }
            catch
            {
                DiskUsedText = "-";
            }
        }

    }
}
