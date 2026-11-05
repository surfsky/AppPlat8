using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using App.Consoler;

namespace App.Pages.Maintains
{
    /// <summary>控制台调度器状态快照。</summary>
    public class ConsolerStatus
    {
        public bool IsRunning { get; set; }
        public int? ProcessId { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? LastOutputAt { get; set; }
        public DateTime? ExitedAt { get; set; }
        public int? LastExitCode { get; set; }
        public string LastError { get; set; }
        public string CommandLine { get; set; }
        public string ApiBaseUrl { get; set; }
        /// <summary>调度器程序集路径（App.Consoler.dll）。</summary>
        public string ConsolerDllPath { get; set; }
        public string WorkingDirectory { get; set; }
        public string ConnectionString { get; set; }
        public string Output { get; set; }
        public int LineCount { get; set; }
        /// <summary>调度器程序集是否存在。</summary>
        public bool HasConsolerDll { get; set; }
        public bool HasConnectionString { get; set; }
    }

    /// <summary>控制台调度器进程管理器。调度器以 dotnet App.Consoler.dll 方式运行，程序集随主站点一起发布。</summary>
    public static class ConsolerRunner
    {
        static readonly object SyncRoot = new();
        static readonly StringBuilder OutputBuffer = new();
        static Process RunningProcess;
        static DateTime? StartedAt;
        static DateTime? LastOutputAt;
        static DateTime? ExitedAt;
        static int? LastExitCode;
        static string LastError;
        static string LastCommandLine;
        static string LastApiBaseUrl;
        static string LastConsolerDllPath;
        static string LastWorkingDirectory;
        static int TotalLineCount;
        const int MaxOutputChars = 200_000;

        // 路径硬编码常量：后续如需支持多副本部署，可改为由调用者传入参数
        /// <summary>调度器程序集文件名（随主站点一起发布到同目录）。</summary>
        const string ConsolerDllName = "App.Consoler.dll";
        /// <summary>读取当前运行状态快照。</summary>
        public static ConsolerStatus GetStatus(string apiBaseUrl)
        {
            lock (SyncRoot)
            {
                var dllPath = GetConsolerDllPath();
                var workingDirectory = GetWorkingDirectory();
                var connectionString = GetConnectionString();
                var isRunning = RunningProcess != null && !RunningProcess.HasExited;
                return new ConsolerStatus
                {
                    IsRunning = isRunning,
                    ProcessId = isRunning ? RunningProcess.Id : null,
                    StartedAt = StartedAt,
                    LastOutputAt = LastOutputAt,
                    ExitedAt = ExitedAt,
                    LastExitCode = LastExitCode,
                    LastError = LastError,
                    CommandLine = LastCommandLine,
                    ApiBaseUrl = string.IsNullOrWhiteSpace(LastApiBaseUrl) ? apiBaseUrl : LastApiBaseUrl,
                    ConsolerDllPath = string.IsNullOrWhiteSpace(LastConsolerDllPath) ? dllPath : LastConsolerDllPath,
                    WorkingDirectory = string.IsNullOrWhiteSpace(LastWorkingDirectory) ? workingDirectory : LastWorkingDirectory,
                    ConnectionString = connectionString,
                    Output = OutputBuffer.ToString(),
                    LineCount = TotalLineCount,
                    HasConsolerDll = File.Exists(dllPath),
                    HasConnectionString = !string.IsNullOrWhiteSpace(connectionString)
                };
            }
        }

        /// <summary>启动调度器进程并开始捕获输出。</summary>
        public static ConsolerStatus Start(string apiBaseUrl, out string error)
        {
            error = null;
            Process process = null;
            ProcessStartInfo startInfo = null;
            string dllPath;
            string workingDirectory;

            lock (SyncRoot)
            {
                if (RunningProcess != null && !RunningProcess.HasExited)
                    return GetStatus(apiBaseUrl);

                dllPath = GetConsolerDllPath();
                workingDirectory = GetWorkingDirectory();

                if (!File.Exists(dllPath))
                {
                    error = $"未找到调度器程序集：{dllPath}";
                    LastError = error;
                    return GetStatus(apiBaseUrl);
                }

                startInfo = BuildStartInfo(dllPath, workingDirectory, apiBaseUrl);
                process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };
                process.OutputDataReceived += (_, e) => AppendOutput(e.Data, false);
                process.ErrorDataReceived += (_, e) => AppendOutput(e.Data, true);
                process.Exited += (_, _) => HandleProcessExited(process);

                StartedAt = DateTime.Now;
                ExitedAt = null;
                LastExitCode = null;
                LastError = null;
                LastApiBaseUrl = apiBaseUrl;
                LastConsolerDllPath = dllPath;
                LastWorkingDirectory = workingDirectory;
                LastCommandLine = BuildCommandLine(dllPath, apiBaseUrl);
                AppendOutput($"[system] 启动调度器：{LastCommandLine}", false);
            }

            try
            {
                if (!process.Start())
                {
                    error = "调度器启动失败";
                    lock (SyncRoot)
                    {
                        LastError = error;
                        AppendOutput($"[system] {error}", true);
                    }
                    return GetStatus(apiBaseUrl);
                }

                lock (SyncRoot)
                {
                    RunningProcess = process;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return GetStatus(apiBaseUrl);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                lock (SyncRoot)
                {
                    LastError = error;
                    AppendOutput($"[system] 启动异常：{error}", true);
                }
                try { process?.Dispose(); } catch { }
                return GetStatus(apiBaseUrl);
            }
        }

        /// <summary>停止正在运行的调度器进程。</summary>
        public static ConsolerStatus Stop(out string error)
        {
            error = null;
            Process process;
            lock (SyncRoot)
            {
                process = RunningProcess;
                if (process == null || process.HasExited)
                    return GetStatus(LastApiBaseUrl);

                AppendOutput("[system] 正在停止调度器...", false);
            }

            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                lock (SyncRoot)
                {
                    LastError = error;
                    AppendOutput($"[system] 停止异常：{error}", true);
                }
            }

            return GetStatus(LastApiBaseUrl);
        }

        /// <summary>清空输出缓存但保留当前进程状态。</summary>
        public static ConsolerStatus ClearOutput(string apiBaseUrl)
        {
            lock (SyncRoot)
            {
                OutputBuffer.Clear();
                TotalLineCount = 0;
                AppendOutput("[system] 输出已清空", false);
            }
            return GetStatus(apiBaseUrl);
        }

        /// <summary>构造 dotnet App.Consoler.dll 启动参数。</summary>
        static ProcessStartInfo BuildStartInfo(string dllPath, string workingDirectory, string apiBaseUrl)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(dllPath);
            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
                psi.ArgumentList.Add($"--api-base={apiBaseUrl}");
            RemoveProxyEnvironment(psi);
            return psi;
        }

        /// <summary>清理可能干扰外网访问的代理环境变量，避免子进程继承 IDE/壳层代理设置。</summary>
        static void RemoveProxyEnvironment(ProcessStartInfo psi)
        {
            var keys = new[]
            {
                "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY",
                "http_proxy", "https_proxy", "all_proxy", "no_proxy",
                "ICUBE_PROXY_HOST"
            };
            foreach (var key in keys)
                psi.Environment.Remove(key);
        }

        /// <summary>拼接人类可读的启动命令文本。</summary>
        static string BuildCommandLine(string dllPath, string apiBaseUrl)
        {
            var parts = new List<string>
            {
                "dotnet",
                $"\"{dllPath}\""
            };
            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
                parts.Add($"--api-base={apiBaseUrl}");
            return string.Join(" ", parts);
        }

        /// <summary>处理外部进程退出事件。</summary>
        static void HandleProcessExited(Process process)
        {
            lock (SyncRoot)
            {
                ExitedAt = DateTime.Now;
                LastExitCode = SafeGetExitCode(process);
                if (ReferenceEquals(RunningProcess, process))
                    RunningProcess = null;
                AppendOutput($"[system] 调度器已退出，ExitCode={LastExitCode}", LastExitCode.GetValueOrDefault() != 0);
            }
            try { process.Dispose(); } catch { }
        }

        /// <summary>安全读取退出码。</summary>
        static int? SafeGetExitCode(Process process)
        {
            try { return process.ExitCode; } catch { return null; }
        }

        /// <summary>追加一行输出，并在超长时裁剪旧内容。</summary>
        static void AppendOutput(string line, bool isError)
        {
            if (line == null)
                return;

            lock (SyncRoot)
            {
                //var prefix = DateTime.Now.ToString("HH:mm:ss");
                var prefix = "";
                var tag = isError ? "[err]" : "";  // out
                OutputBuffer.Append(prefix).Append(' ').Append(tag).Append(' ').AppendLine(line);
                LastOutputAt = DateTime.Now;
                TotalLineCount++;
                TrimOutputIfNeeded();
            }
        }

        /// <summary>裁剪过长输出，避免页面和进程常驻内存无限增长。</summary>
        static void TrimOutputIfNeeded()
        {
            if (OutputBuffer.Length <= MaxOutputChars)
                return;

            var removeLength = OutputBuffer.Length - MaxOutputChars;
            var removeTo = OutputBuffer.ToString().IndexOf('\n', Math.Max(removeLength, 1));
            if (removeTo < 0)
                removeTo = removeLength;
            OutputBuffer.Remove(0, Math.Min(removeTo + 1, OutputBuffer.Length));
        }

        /// <summary>解析调度器程序集路径（与主站点同目录发布）。</summary>
        static string GetConsolerDllPath()
        {
            return Path.Combine(AppContext.BaseDirectory, ConsolerDllName);
        }

        /// <summary>解析调度器工作目录（与主站点程序目录一致）。</summary>
        static string GetWorkingDirectory()
        {
            return AppContext.BaseDirectory;
        }

        /// <summary>从 App.Consoler 配置文件读取数据库连接字符串。</summary>
        static string GetConnectionString()
        {
            try
            {
                return ConsolerConfig.Parse(Array.Empty<string>()).Conn ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
