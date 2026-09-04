using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Serilog;
using Serilog.Core;
using App.DAL;
using App.Web;
using App.Utils;

namespace App.Components
{
    /// <summary>
    /// 日志器
    /// </summary>
    public class Logger
    {
        static Serilog.Core.Logger _log = new Lazy<Serilog.Core.Logger>(() => CreateLogger()).Value;
        static Serilog.Core.Logger CreateLogger()
        {
            var sp = Path.DirectorySeparatorChar;
            return new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File($"Logs{sp}log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger()
                ;
        }

        // 记录日志
        public static void Info(string format, params object[] ps) => _log.Information(format, ps);
        public static void Error(string format, params object[] ps) => _log.Error(format, ps);
        public static void Fatal(string format, params object[] ps) => _log.Fatal(format, ps);
        public static void Warn(string format, params object[] ps) => _log.Warning(format, ps);


        //------------------------------------------------------
        // 数据库日志
        //------------------------------------------------------
        /// <summary>保存到数据库（完整参数）</summary>
        /// <param name="level">等级</param>
        /// <param name="user">用户名</param>
        /// <param name="message">信息</param>
        /// <param name="from">来自那个客户端/模块</param>
        public static void LogDb(LogLevel level, string user, string message, string from)
        {
            var log = new DAL.Log();
            log.LogDt = DateTime.Now;
            log.Level = level;
            log.Message = message;
            log.Summary = message.Summary(256);
            log.From = from;
            log.Operator = user;
            if (Asp.Request != null)
            {
                log.URL = Asp.Url;
                log.IP = Asp.ClientIP;
                log.Method = Asp.Request.Method;
                log.Referrer = Asp.GetUrlReferrer();
            }
            log.Save();
        }

        /// <summary>保存到数据库（自动取当前登录用户名）</summary>
        public static void LogDb(LogLevel level, string message, string from)
            => LogDb(level, user: Auth.GetUserName(Asp.Current) ?? "", message: message, from: from);


        /// <summary>审计日志（Info）统一入口：module 形如 "Checks/CheckObjects"，action 形如 "Delete/Save/Import/Export/Login/Logout"，自动拼接 From=module+action </summary>
        public static void LogAudit(string module, string action, string message)
            => LogDb(LogLevel.Info, message, from: $"{module}/{action}");

        /// <summary>记录实体删除审计：自动构造 "删除 [类型]: 主键,名称" 消息；传入 ids/names 列表</summary>
        public static void LogDelete<T>(string module, IList<long> ids, IList<string> names = null)
        {
            if (ids == null || ids.Count == 0) return;
            var typeName = typeof(T).Name;
            string detail;
            if (names != null && names.Count == ids.Count)
            {
                var parts = ids.Zip(names, (id, name) => $"Id={id}|Name={name}");
                detail = string.Join(" ; ", parts);
            }
            else
            {
                detail = $"Ids=[{string.Join(",", ids)}]";
            }
            LogAudit(module, "Delete", $"删除 [{typeName}] 共 {ids.Count} 条。明细: {detail}");
        }

        /// <summary>记录导出审计：模块 + 文件名 + 条数</summary>
        public static void LogExport(string module, string fileName, int count)
            => LogAudit(module, "Export", $"导出数据 {count} 条，文件: {fileName}");

        /// <summary>记录导入审计：模块 + 文件名 + 成功数</summary>
        public static void LogImport(string module, string fileName, int success, int failed = 0)
            => LogAudit(module, "Import", $"导入数据：成功 {success} 条，失败 {failed} 条，文件: {fileName}");
    }
}
