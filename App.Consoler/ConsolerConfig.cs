using App.Utils;
using System.Collections.ObjectModel;
using System.Data.Common;

namespace App.Consoler
{
    /// <summary>
    /// 调度器运行配置。
    /// 数据库连接串仅从 App.Consoler.Config.json 读取，其它参数允许命令行覆盖。
    /// 采用独立文件名，避免与主站点 appsettings.json 在同一输出目录下冲突。
    /// </summary>
    public sealed class ConsolerConfig
    {
        // const
        public const string ConfigFileName = "App.Consoler.Config.json";
        public const string DefaultApiBase = "http://localhost:6060";
        public const string DefaultAmapKey = "";
        static readonly object InstanceLock = new();
        static volatile ConsolerConfig? _instance;
        readonly Dictionary<string, string> _extras;

        ConsolerConfig(ConfigFile? file)
        {
            ApiBase = file?.ApiBase ?? string.Empty;
            ConnStr = file?.Conn ?? string.Empty;
            AmapKey = file?.AmapKey ?? string.Empty;
            RunJob = file?.RunJob ?? string.Empty;
            _extras = new Dictionary<string, string>(file?.Extras ?? new(), StringComparer.OrdinalIgnoreCase);
            Extras = new ReadOnlyDictionary<string, string>(_extras);
        }

        /// <summary>启动时初始化的全局配置；未初始化时访问会抛出异常。</summary>
        public static ConsolerConfig Instance => _instance
            ?? throw new InvalidOperationException("ConsolerConfig 尚未初始化。");

        /// <summary>解析并初始化全局配置，进程中只能初始化一次。</summary>
        public static ConsolerConfig Initialize(string[] args)
        {
            lock (InstanceLock)
            {
                if (_instance != null)
                    throw new InvalidOperationException("ConsolerConfig 已初始化。");
                return _instance = Parse(args);
            }
        }

        /// <summary>站点 API 基础地址（供 Job 回调）</summary>
        public string ApiBase { get; private set; }

        /// <summary>数据库连接字符串</summary>
        public string ConnStr { get; private set; }

        /// <summary>高德地图 API Key</summary>
        public string AmapKey { get; private set; }

        /// <summary>仅运行指定任务（设置后为单次执行模式，不启动调度器）</summary>
        public string RunJob { get; private set; }

        /// <summary>透传给 Job 的额外参数（命令行中未识别的 --key=value）</summary>
        public IReadOnlyDictionary<string, string> Extras { get; }

        /// <summary>配置文件物理路径（与程序同目录）</summary>
        public static string ConfigFilePath => Path.Combine(AppContext.BaseDirectory, ConfigFileName);

        //-------------------------------------------------
        // 解析
        //-------------------------------------------------
        /// <summary>解析配置：先读配置文件，再用命令行参数覆盖非数据库项，最后补默认值并规范化路径</summary>
        public static ConsolerConfig Parse(string[] args) => Parse(args, ConfigFilePath);

        internal static ConsolerConfig Parse(string[] args, string configFilePath)
        {
            var config = new ConsolerConfig(LoadFromFile(configFilePath));
            config.OverrideByArgs(args);
            config.ApplyDefaults();
            config.NormalizeConnectionString();
            return config;
        }

        /// <summary>从同目录配置文件反序列化（文件缺失或损坏时返回 null）</summary>
        static ConfigFile? LoadFromFile(string configFilePath)
        {
            if (!File.Exists(configFilePath))
                return null;

            try
            {
                return JsonHelper.LoadJsonFile<ConfigFile>(configFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[配置] 解析 {configFilePath} 失败，已忽略：{ex.Message}");
                return null;
            }
        }

        /// <summary>用命令行参数覆盖配置项（未识别的参数收集到 Extras 透传给 Job）</summary>
        void OverrideByArgs(string[] args)
        {
            if (args == null)
                return;

            foreach (var arg in args)
            {
                var pair = SplitArg(arg);
                if (pair == null)
                    continue;

                var key = pair.Value.Key;
                var value = pair.Value.Value;
                if (key.Equals("api-base", StringComparison.OrdinalIgnoreCase)) ApiBase = value;
                else if (key.Equals("conn", StringComparison.OrdinalIgnoreCase)) continue;
                else if (key.Equals("amap-key", StringComparison.OrdinalIgnoreCase)) AmapKey = value;
                else if (key.Equals("run", StringComparison.OrdinalIgnoreCase)) RunJob = value;
                else _extras[key] = value;
            }
        }

        /// <summary>为空配置项填充默认值</summary>
        void ApplyDefaults()
        {
            if (string.IsNullOrWhiteSpace(ApiBase)) ApiBase = DefaultApiBase;
            if (string.IsNullOrWhiteSpace(AmapKey)) AmapKey = DefaultAmapKey;
        }

        /// <summary>把连接串中的相对数据库路径折算为相对当前 DLL 目录的绝对路径。</summary>
        void NormalizeConnectionString()
        {
            if (string.IsNullOrWhiteSpace(ConnStr))
                return;

            try
            {
                var builder = new DbConnectionStringBuilder
                {
                    ConnectionString = ConnStr
                };

                NormalizePathItem(builder, "Data Source");
                NormalizePathItem(builder, "DataSource");
                NormalizePathItem(builder, "Filename");

                ConnStr = builder.ConnectionString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[配置] 规范化数据库连接串失败，已保留原值：{ex.Message}");
            }
        }

        /// <summary>规范化连接串中的单个路径字段。</summary>
        static void NormalizePathItem(DbConnectionStringBuilder builder, string key)
        {
            if (!builder.ContainsKey(key))
                return;

            var raw = builder[key]?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var path = raw.Trim().Trim('"');
            if (!ShouldResolveRelativePath(path))
                return;

            builder[key] = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        }

        /// <summary>判断是否需要把路径解释为相对当前 DLL 目录。</summary>
        static bool ShouldResolveRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (path.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
                return false;

            if (path.StartsWith("|DataDirectory|", StringComparison.OrdinalIgnoreCase))
                return false;

            if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                return false;

            return !Path.IsPathRooted(path);
        }

        /// <summary>把 --key=value 形式的参数拆成键值对；格式不符时返回 null</summary>
        static (string Key, string Value)? SplitArg(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith("--", StringComparison.Ordinal))
                return null;

            var idx = arg.IndexOf('=');
            if (idx <= 2)
                return null;

            return (arg.Substring(2, idx - 2).Trim(), arg.Substring(idx + 1).Trim());
        }

        // 文件模型仅用于反序列化，运行时不暴露可写属性。
        sealed class ConfigFile
        {
            public string? ApiBase { get; set; }
            public string? Conn { get; set; }
            public string? AmapKey { get; set; }
            public string? RunJob { get; set; }
            public Dictionary<string, string>? Extras { get; set; }
        }
    }
}
