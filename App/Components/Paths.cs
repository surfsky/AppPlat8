using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using App.Web;
using App.Utils;

namespace App.Components
{
    /// <summary>应用路径统一入口：只负责从配置文件解析路径并打印摘要。</summary>
    public static class Paths
    {
        public static string ConnectionString;  // 数据库连接字符串
        public static string ConfigPath;  // 配置文件路径

        // 物理路径
        public static string DataRoot;    // 数据根目录
        public static string LogRoot;     // 日志根目录
        public static string FileRoot;    // 文件根目录
        public static string CacheRoot;   // 缓存根目录

        // 子目录
        public static string LogFolder;
        public static string FileFolder;
        public static string CacheFolder;



        /// <summary>类型初始化时加载配置并打印路径摘要。</summary>
        static Paths()
        {
            Load();
        }


        /// <summary>返回可直接打印的路径摘要。</summary>
        public static string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Storage Paths:");
            sb.AppendLine($"  ConfigPath  = {ConfigPath}");
            sb.AppendLine($"  DataRoot    = {DataRoot}");
            sb.AppendLine($"  LogRoot     = {LogRoot}");
            sb.AppendLine($"  FileRoot    = {FileRoot}");
            sb.AppendLine($"  CacheRoot   = {CacheRoot}");
            sb.AppendLine($"  ConnectionString = {ConnectionString}");
            return sb.ToString().TrimEnd();
        }

        //---------------------------------------------------------------------
        // File
        //---------------------------------------------------------------------
        /// <summary>获取物理文件路径。</summary>
        /// <param name="virtualFilePath">虚拟文件路径，相对于 FileRoot。</param>
        /// <returns>物理绝对路径。</returns>
        public static string GetFilePath(string virtualFilePath)
        {
            var relativePath = virtualFilePath.TrimStart("/Files/").TrimStart('/');
            var p =  Path.Combine(FileRoot, relativePath);
            return p;
            //return Asp.MapPath(p);
            //return Path.GetFullPath(p);
        }


        //---------------------------------------------------------------------
        // Config file
        //---------------------------------------------------------------------
        /// <summary>重新从配置文件加载路径。</summary>
        public static void Load()
        {
            var section = LoadConfig()?["Paths"] as JsonObject;
            LogFolder   = section?["LogFolder"]?.ToString() ?? "Logs";
            FileFolder  = section?["FileFolder"]?.ToString() ?? "Files";
            CacheFolder = section?["CacheFolder"]?.ToString() ?? "Caches";

            DataRoot   = GetPhysicalPath("", section?["DataRoot"]?.ToString());
            LogRoot    = GetPhysicalPath(DataRoot, LogFolder);
            FileRoot   = GetPhysicalPath(DataRoot, FileFolder);
            CacheRoot  = GetPhysicalPath(DataRoot, CacheFolder);
        }


        /// <summary>读取 appsettings.json 根节点。</summary>
        private static JsonObject LoadConfig()
        {
            try
            {
                ConfigPath = GetConfigPath();
                using var sr = new StreamReader(ConfigPath, Encoding.UTF8);
                return JsonNode.Parse(sr.ReadToEnd()) as JsonObject;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PATHS][WARN] parse appsettings.json failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>获取配置文件所在目录：优先当前目录，最后程序目录。</summary>
        private static string GetConfigPath()
        {
            var cfgPath1 = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            var cfgPath2 = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            return File.Exists(cfgPath1) ? cfgPath1 : cfgPath2;
        }

        /// <summary>把配置路径转成物理绝对路径</summary>
        private static string GetPhysicalPath(string root, string subFolder)
        {
            var path = Path.IsPathRooted(subFolder) ? subFolder : Path.Combine(root, subFolder);
            return Path.GetFullPath(path);
        }
    }
}
