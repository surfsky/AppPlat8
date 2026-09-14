using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace App.Utils
{
    /// <summary>
    /// 应用存储路径统一入口
    /// 设计优先级：appsettings.json 节 → 默认相对源目录 → 网站项目根目录（而不是 bin/发布目录）
    /// 所有散写（Files / Logs / Caches 目录）统一走这里；Db/数据库位置由 ConnectionStrings 接管。
    /// </summary>
    public static class Paths
    {
        //-----------------------------------------------------
        // 外部可用：内容根 + 目录（保证目录存在，带一次性写测试）
        //-----------------------------------------------------
        public static string ContentRoot { get { EnsureInit(); return _contentRoot; } }
        public static string DbRoot      { get { EnsureInit(); EnsureDir(_dbRoot);     return _dbRoot; } }
        public static string LogsRoot    { get { EnsureInit(); EnsureDir(_logsRoot);   return _logsRoot; } }
        public static string FilesRoot   { get { EnsureInit(); EnsureDir(_filesRoot);  return _filesRoot; } }
        public static string CachesRoot  { get { EnsureInit(); EnsureDir(_cachesRoot); return _cachesRoot; } }


        //-----------------------------------------------------
        // 静态字段：ContentRoot / 配置 / 一次性初始化
        //-----------------------------------------------------
        private static readonly object _lock = new object();
        private static bool _inited;
        private static string _contentRoot;
        private static string _dbRoot;
        private static string _logsRoot;
        private static string _filesRoot;
        private static string _cachesRoot;

        private const string DefaultLogsDir   = "Logs";
        private const string DefaultFilesDir  = "Files";
        private const string DefaultCachesDir = "Caches";
        private const string DefaultDbDir     = "Db";


        // 致命目录黑名单：绝不允许落到这些位置（避免管理员手滑填系统目录造成覆盖）
        private static readonly string[] DangerousRootMarkers = new[]
        {
            "/etc/", "/boot/", "/proc/", "/sys/", "/dev/",
            "\\windows\\", "\\system32\\", "\\program files\\",
        };

        //-----------------------------------------------------
        // 初始化
        //-----------------------------------------------------
        private static void EnsureInit()
        {
            if (_inited) return;
            lock (_lock)
            {
                if (_inited) return;
                try { InitCore(); }
                catch (Exception ex)
                {
                    // 即使这里出问题也要有兜底，不能把应用搞挂（启动早期 Serilog 可能还没建起来）
                    Console.Error.WriteLine($"[PATHS][FATAL] init failed, fallback to CurrentDirectory. err={ex.Message}");
                    _contentRoot = SafeFull(Directory.GetCurrentDirectory());
                    _dbRoot     = Path.Combine(_contentRoot, DefaultDbDir);
                    _logsRoot   = Path.Combine(_contentRoot, DefaultLogsDir);
                    _filesRoot  = Path.Combine(_contentRoot, DefaultFilesDir);
                    _cachesRoot = Path.Combine(_contentRoot, DefaultCachesDir);
                }
                _inited = true;
            }
        }

        private static void InitCore()
        {
            _contentRoot = ResolveContentRoot();

            // 读取 appsettings.json Paths 节
            var (logsCfg, filesCfg, cachesCfg) = (null as string, null as string, null as string);
            var cfgPath = Path.Combine(_contentRoot, "appsettings.json");
            if (File.Exists(cfgPath))
            {
                try
                {
                    using var sr = new StreamReader(cfgPath, Encoding.UTF8);
                    var root = JsonNode.Parse(sr.ReadToEnd()) as JsonObject;
                    if (root?["Paths"] is JsonObject section)
                    {
                        logsCfg   = section["LogsPath"]?.ToString();
                        filesCfg  = section["FilesPath"]?.ToString();
                        cachesCfg = section["CachesPath"]?.ToString();
                    }
                }
                catch (Exception ex) { Console.Error.WriteLine($"[PATHS][WARN] parse appsettings.json failed: {ex.Message}"); }
            }

            // Db 位置由 ConnectionStrings 管理；这里 DbRoot/DbFile 仅做辅助用（后台 console 工具找 db 文件 fallback）
            _dbRoot     = Path.Combine(_contentRoot, DefaultDbDir);
            _logsRoot   = ResolveDir(logsCfg,   DefaultLogsDir);
            _filesRoot  = ResolveDir(filesCfg,  DefaultFilesDir);
            _cachesRoot = ResolveDir(cachesCfg, DefaultCachesDir);
        }

        //-----------------------------------------------------
        // ContentRoot 探测（优先项目源目录，其次 bin 发布目录）
        //   判断源目录标准："有 appsettings.json 且至少存在 2 个真实源目录(Files/Logs/Caches/Db 中任两个)"
        //   这样开发环境从 bin/Debug/net8.0 启动时也能回溯到真正的 App/ 项目目录
        //-----------------------------------------------------
        private static string ResolveContentRoot()
        {
            var allCandidates = new System.Collections.Generic.List<string>();
            CollectCandidates(allCandidates, Directory.GetCurrentDirectory());
            CollectCandidates(allCandidates, AppContext.BaseDirectory);

            //   a) 第一轮：命中"有 appsettings.json 且看起来像源项目目录（含 .csproj / Pages / wwwroot / Migrations 任意一个）"
            foreach (var d in allCandidates)
            {
                if (HasAppSettings(d) && LooksLikeSourceProject(d))
                    return SafeFull(d);
            }
            //   b) 第二轮：命中"有 appsettings.json 且 >=2 个真实源目录"
            foreach (var d in allCandidates)
            {
                if (HasAppSettings(d) && CountSourceDirs(d) >= 2)
                    return SafeFull(d);
            }
            //   c) 第三轮：命中"有 appsettings.json"（纯发布目录，源目录是符号链接或未复制到）
            foreach (var d in allCandidates)
            {
                if (HasAppSettings(d))
                    return SafeFull(d);
            }
            //   d) 兜底：进程当前目录
            return SafeFull(Directory.GetCurrentDirectory());
        }

        private static void CollectCandidates(System.Collections.Generic.List<string> list, string startDir)
        {
            var d = startDir;
            for (int i = 0; i < 12; i++)
            {
                if (!string.IsNullOrEmpty(d) && Directory.Exists(d) && !list.Contains(d, StringComparer.OrdinalIgnoreCase))
                    list.Add(d);
                var parent = Directory.GetParent(d);
                if (parent == null) break;
                d = parent.FullName;
            }
        }
        private static bool HasAppSettings(string dir) => File.Exists(Path.Combine(dir, "appsettings.json"));
        private static bool LooksLikeSourceProject(string dir)
        {
            // 源项目目录标记（只有源代码目录有，bin/发布目录绝不会有）：同层级存在 .csproj 文件
            // （Pages/wwwroot 会在 Publish 时被复制到 bin，不能作为源项目标记）
            try { foreach (var _ in Directory.EnumerateFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly)) return true; } catch {}
            return false;
        }
        private static int CountSourceDirs(string dir)
        {
            int n = 0;
            if (Directory.Exists(Path.Combine(dir, "Db")))     n++;
            if (Directory.Exists(Path.Combine(dir, "Logs")))   n++;
            if (Directory.Exists(Path.Combine(dir, "Files")))  n++;
            if (Directory.Exists(Path.Combine(dir, "Caches"))) n++;
            return n;
        }

        /// <summary>把配置值解析为安全绝对路径；null 用默认相对目录；命中黑名单抛错</summary>
        private static string ResolveDir(string cfgValue, string defaultRelDir)
        {
            string raw;
            if (string.IsNullOrWhiteSpace(cfgValue))
                raw = Path.Combine(_contentRoot, defaultRelDir);
            else if (Path.IsPathRooted(cfgValue))
                raw = cfgValue;
            else
                raw = Path.Combine(_contentRoot, cfgValue);

            raw = SafeFull(raw);
            ThrowIfDangerous(raw);
            return raw;
        }

        /// <summary>把路径转换为安全绝对路径；null 用当前目录</summary>
        private static string SafeFull(string p)
        {
            try { return Path.GetFullPath(string.IsNullOrWhiteSpace(p) ? Directory.GetCurrentDirectory() : p); }
            catch { return p; }
        }

        /// <summary>检查路径是否危险（容量撑爆风险 + 权限太大）</summary>
        private static void ThrowIfDangerous(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var lower = path.Replace('\\', '/').ToLower() + "/";
            // 单个根（/ 或 C:\）直接拒绝：容量撑爆风险 + 权限太大
            if (lower == "/" || lower.Length == 3 && lower[1] == ':' && (lower[2] == '/' || lower[2] == '\\'))
                throw new InvalidOperationException($"[PATHS] Refuse root directory: {path}");
            foreach (var m in DangerousRootMarkers)
                if (lower.StartsWith(m.Replace('\\', '/')) || lower.Contains(m))
                    throw new InvalidOperationException($"[PATHS] Refuse dangerous location: {path} hits marker {m}");
        }


        //-----------------------------------------------------
        // 便捷拼接
        //-----------------------------------------------------
        public static string CombineWithDb(params string[] parts)     => CombinePrepend(DbRoot, parts);
        public static string CombineWithLogs(params string[] parts)   => CombinePrepend(LogsRoot, parts);
        public static string CombineWithFiles(params string[] parts)  => CombinePrepend(FilesRoot, parts);
        public static string CombineWithCaches(params string[] parts) => CombinePrepend(CachesRoot, parts);
        public static string Combine(string root, params string[] parts)
        {
            if (parts == null || parts.Length == 0) return root ?? "";
            var all = new string[parts.Length + 1];
            all[0] = root;
            Array.Copy(parts, 0, all, 1, parts.Length);
            return SafeFull(Path.Combine(all.Where(s => !string.IsNullOrEmpty(s)).ToArray()));
        }
        private static string CombinePrepend(string root, string[] parts)
        {
            if (parts == null || parts.Length == 0) return root;
            var all = new string[parts.Length + 1];
            all[0] = root;
            Array.Copy(parts, 0, all, 1, parts.Length);
            return SafeFull(Path.Combine(all.Where(s => !string.IsNullOrEmpty(s)).ToArray()));
        }

        //-----------------------------------------------------
        // Asp.MapPath 前缀识别：当路径以 /Files/ 或 /Caches/ 开头时，
        // 从虚拟路径后缀直接拼为真实物理路径，不再相对 ContentRoot。
        // （这样 Uploader / ImageMiddleware 无需改动，只要 Asp.MapPath 调这里即可）
        //-----------------------------------------------------
        /// <summary>若 virtualPath 是 /Files/... /Caches/... 前缀，直接映射到对应物理根；否则返回 null 交给上层默认逻辑。</summary>
        public static string TryMapKnownPrefix(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath)) return null;
            var p = virtualPath.TrimStart('~').Replace('\\', '/');
            if (p.StartsWith("/Files/", StringComparison.OrdinalIgnoreCase))
                return CombineWithFiles(p.Substring("/Files/".Length).Split('/'));
            if (p.StartsWith("/Caches/", StringComparison.OrdinalIgnoreCase))
                return CombineWithCaches(p.Substring("/Caches/".Length).Split('/'));
            return null;
        }

        //-----------------------------------------------------
        // 显式注入（Program.cs / 单元测试启动时可选调用）
        //-----------------------------------------------------
        /// <summary>在 Init 前显式指定内容根目录（例如 Program.cs 中 builder.Environment.ContentRootPath）</summary>
        public static void SetContentRoot(string contentRoot)
        {
            if (string.IsNullOrWhiteSpace(contentRoot)) return;
            if (_inited) { Console.Error.WriteLine($"[PATHS][WARN] SetContentRoot called after init; ignored value={contentRoot}"); return; }
            Environment.SetEnvironmentVariable("APPPLAT_CONTENT_ROOT", contentRoot);
        }

        //-----------------------------------------------------
        // 生命周期：创建目录 + 写测试 + 汇总日志
        //-----------------------------------------------------
        private static void EnsureDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); }
            catch (Exception ex) { Console.Error.WriteLine($"[PATHS][FATAL] cannot create dir {dir}: {ex.Message}"); throw; }
        }

        /// <summary>确保目录全部存在，并对 Files / Logs / Caches 做一次性写测试（立即发现权限不足）。返回可打印的汇总字符串</summary>
        public static string EnsureAll(bool testWritable = true)
        {
            EnsureInit();
            EnsureDir(_dbRoot); EnsureDir(_logsRoot); EnsureDir(_filesRoot); EnsureDir(_cachesRoot);

            string writeTest = "";
            if (testWritable)
            {
                var ts = DateTime.Now.Ticks.ToString("x");
                writeTest = string.Join("; ",
                    TestWrite(LogsRoot,   $"_writable_test_{ts}.tmp"),
                    TestWrite(FilesRoot,  $"_writable_test_{ts}.tmp"),
                    TestWrite(CachesRoot, $"_writable_test_{ts}.tmp"));
            }
            return DumpSummary(writeTest);
        }

        private static string TestWrite(string dir, string tmpName)
        {
            var fp = Path.Combine(dir, tmpName);
            try
            {
                File.WriteAllText(fp, "ok");
                if (File.Exists(fp)) File.Delete(fp);
                return $"{NameOf(dir)}:OK";
            }
            catch (Exception ex)
            {
                return $"{NameOf(dir)}:FAIL({ex.Message})";
            }
        }
        private static string NameOf(string dir)
        {
            if (dir == _dbRoot) return "Db";
            if (dir == _logsRoot) return "Logs";
            if (dir == _filesRoot) return "Files";
            if (dir == _cachesRoot) return "Caches";
            return dir;
        }

        /// <summary>返回可直接打印到启动日志的汇总信息（方便运维核对备份目录）</summary>
        public static string DumpSummary(string extra = "")
        {
            EnsureInit();
            var sb = new StringBuilder();
            sb.AppendLine("Storage Paths:");
            sb.AppendLine($"  ContentRoot = {_contentRoot}");
            sb.AppendLine($"  DbRoot      = {_dbRoot}     (managed by ConnectionStrings; fallback helper)");
            sb.AppendLine($"  LogsRoot    = {_logsRoot}");
            sb.AppendLine($"  FilesRoot   = {_filesRoot}");
            sb.AppendLine($"  CachesRoot  = {_cachesRoot}");
            if (!string.IsNullOrEmpty(extra))
                sb.AppendLine($"  Write Test  = {extra}");
            return sb.ToString().TrimEnd();
        }
    }
}
