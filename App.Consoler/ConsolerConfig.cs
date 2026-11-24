using App.Utils;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Text.Json.Serialization;

namespace App.Consoler
{
    /// <summary>
    /// 调度器运行配置。直接反序列化 App.Consoler.Config.json，
    /// 数据库连接串仅从文件读取，其它参数允许命令行覆盖。
    /// </summary>
    public sealed class ConsolerConfig
    {
        public string ApiBase { get; set; } = string.Empty;
        [JsonPropertyName("conn")]
        public string ConnStr { get; set; } = string.Empty;
        public string AmapKey { get; set; } = string.Empty;
        public string RunJob { get; set; } = string.Empty;
        public Dictionary<string, string> Extras { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public const string ConfigFileName = "App.Consoler.Config.json";
        public const string DefaultApiBase = "http://localhost:6060";
        public const string DefaultAmapKey = "";
        static readonly object InstanceLock = new();
        static volatile ConsolerConfig? _instance;

        public static string ConfigFilePath => Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        public static ConsolerConfig Instance => _instance ?? throw new InvalidOperationException("ConsolerConfig 尚未初始化。");


        //-------------------------------------------------
        // 初始化
        //-------------------------------------------------
        ConsolerConfig() { }

        /// <summary>启动时初始化全局配置；进程中只能初始化一次。</summary>
        public static ConsolerConfig Initialize(string[] args)
        {
            lock (InstanceLock)
            {
                if (_instance != null)
                    throw new InvalidOperationException("ConsolerConfig 已初始化。");
                return _instance = Parse(args);
            }
        }


        //-------------------------------------------------
        // 解析
        //-------------------------------------------------
        /// <summary>解析配置：先读配置文件，再用命令行覆盖，最后补默认值并规范化路径</summary>
        public static ConsolerConfig Parse(string[] args) => Parse(args, ConfigFilePath);

        internal static ConsolerConfig Parse(string[] args, string configFilePath)
        {
            var cfg = LoadFromFile(configFilePath) ?? new ConsolerConfig();
            cfg.OverrideByArgs(args);
            cfg.ApplyDefaults();
            cfg.NormalizeConnectionString();
            return cfg;
        }

        /// <summary>从同目录配置文件反序列化（文件缺失或损坏时返回 null）</summary>
        static ConsolerConfig? LoadFromFile(string configFilePath)
        {
            if (!File.Exists(configFilePath))
                return null;

            try
            {
                return JsonHelper.LoadJsonFile<ConsolerConfig>(configFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[配置] 解析 {configFilePath} 失败，已忽略：{ex.Message}");
                return null;
            }
        }

        //-------------------------------------------------
        // 解析命令行参数
        //-------------------------------------------------
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
                if      (key.Equals("api-base", StringComparison.OrdinalIgnoreCase))  ApiBase = value;
                else if (key.Equals("conn", StringComparison.OrdinalIgnoreCase))      continue;
                else if (key.Equals("amap-key", StringComparison.OrdinalIgnoreCase))  AmapKey = value;
                else if (key.Equals("run", StringComparison.OrdinalIgnoreCase))       RunJob = value;
                else Extras[key] = value;
            }
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


        /// <summary>为空配置项填充默认值</summary>
        void ApplyDefaults()
        {
            if (string.IsNullOrWhiteSpace(ApiBase)) ApiBase = DefaultApiBase;
            if (string.IsNullOrWhiteSpace(AmapKey)) AmapKey = DefaultAmapKey;
        }

        //-------------------------------------------------
        // 解析数据库连接串
        //-------------------------------------------------
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
    }
}