using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

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
        public string ProjectPath { get; set; }
        public string WorkingDirectory { get; set; }
        public string DbPath { get; set; }
        public string Output { get; set; }
        public int LineCount { get; set; }
        public bool HasProject { get; set; }
        public bool HasDatabase { get; set; }
    }
    
    /// <summary>控制台调度器进程管理器。</summary>
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
        static string LastProjectPath;
        static string LastWorkingDirectory;
        static int TotalLineCount;
        const int MaxOutputChars = 200_000;

        // 路径硬编码常量：后续如需支持多副本部署，可改为由调用者传入参数
        /// <summary>调度器项目目录名（相对解决方案根目录）。</summary>
        const string ConsolerProjectDir = "App.Consoler";
        /// <summary>调度器项目文件名。</summary>
        const string ConsolerProjectFile = "App.Consoler.csproj";
        /// <summary>默认数据库相对路径（相对解决方案根目录）。</summary>
        const string DatabaseRelativePath = "App/Db/sqlite.db";

        /// <summary>读取当前运行状态快照。</summary>
        public static ConsolerStatus GetStatus(string apiBaseUrl)
        {
            lock (SyncRoot)
            {
                var solutionRoot = ResolveSolutionRoot();
                var projectPath = ResolveProjectPath(solutionRoot);
                var workingDirectory = ResolveWorkingDirectory(solutionRoot);
                var dbPath = ResolveDbPath(solutionRoot);
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
                    ProjectPath = string.IsNullOrWhiteSpace(LastProjectPath) ? projectPath : LastProjectPath,
                    WorkingDirectory = string.IsNullOrWhiteSpace(LastWorkingDirectory) ? workingDirectory : LastWorkingDirectory,
                    DbPath = dbPath,
                    Output = OutputBuffer.ToString(),
                    LineCount = TotalLineCount,
                    HasProject = System.IO.File.Exists(projectPath),
                    HasDatabase = System.IO.File.Exists(dbPath)
                };
            }
        }

        /// <summary>启动调度器进程并开始捕获输出。</summary>
        public static ConsolerStatus Start(string apiBaseUrl, out string error)
        {
            error = null;
            Process process = null;
            ProcessStartInfo startInfo = null;
            string solutionRoot;
            string projectPath;
            string workingDirectory;
            string dbPath;

            lock (SyncRoot)
            {
                if (RunningProcess != null && !RunningProcess.HasExited)
                    return GetStatus(apiBaseUrl);

                solutionRoot = ResolveSolutionRoot();
                projectPath = ResolveProjectPath(solutionRoot);
                workingDirectory = ResolveWorkingDirectory(solutionRoot);
                dbPath = ResolveDbPath(solutionRoot);

                if (!System.IO.File.Exists(projectPath))
                {
                    error = $"未找到调度器项目：{projectPath}";
                    LastError = error;
                    return GetStatus(apiBaseUrl);
                }

                startInfo = BuildStartInfo(projectPath, workingDirectory, dbPath, apiBaseUrl);
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
                LastProjectPath = projectPath;
                LastWorkingDirectory = workingDirectory;
                LastCommandLine = BuildCommandLine(projectPath, dbPath, apiBaseUrl);
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

        /// <summary>构造 dotnet run 启动参数。</summary>
        static ProcessStartInfo BuildStartInfo(string projectPath, string workingDirectory, string dbPath, string apiBaseUrl)
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
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(projectPath);
            psi.ArgumentList.Add("--");
            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
                psi.ArgumentList.Add($"--api-base={apiBaseUrl}");
            if (System.IO.File.Exists(dbPath))
                psi.ArgumentList.Add($"--conn=Data Source={dbPath};");
            return psi;
        }

        /// <summary>拼接人类可读的启动命令文本。</summary>
        static string BuildCommandLine(string projectPath, string dbPath, string apiBaseUrl)
        {
            var parts = new List<string>
            {
                "dotnet run",
                $"--project \"{projectPath}\"",
                "--"
            };
            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
                parts.Add($"--api-base={apiBaseUrl}");
            if (System.IO.File.Exists(dbPath))
                parts.Add($"--conn=\"Data Source={dbPath};\"");
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
                var prefix = DateTime.Now.ToString("HH:mm:ss");
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

        /// <summary>定位解决方案根目录。</summary>
        static string ResolveSolutionRoot()
        {
            var candidates = new List<string>();
            AddCandidate(candidates, Directory.GetCurrentDirectory());
            AddCandidate(candidates, AppContext.BaseDirectory);

            var current = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && current != null; i++)
            {
                AddCandidate(candidates, current.FullName);
                current = current.Parent;
            }

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (System.IO.File.Exists(Path.Combine(candidate, ConsolerProjectDir, ConsolerProjectFile)))
                    return candidate;
            }

            return Directory.GetCurrentDirectory();
        }

        /// <summary>收集可能的根目录候选项。</summary>
        static void AddCandidate(List<string> candidates, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            try
            {
                candidates.Add(Path.GetFullPath(path));
            }
            catch
            {
                // ignore invalid path
            }
        }

        /// <summary>解析调度器项目文件路径。</summary>
        static string ResolveProjectPath(string solutionRoot)
        {
            return Path.Combine(solutionRoot, ConsolerProjectDir, ConsolerProjectFile);
        }

        /// <summary>解析调度器工作目录。</summary>
        static string ResolveWorkingDirectory(string solutionRoot)
        {
            return solutionRoot;
        }

        /// <summary>解析默认数据库文件路径。</summary>
        static string ResolveDbPath(string solutionRoot)
        {
            return Path.Combine(solutionRoot, DatabaseRelativePath);
        }
    }
}
