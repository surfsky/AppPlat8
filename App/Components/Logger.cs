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
        public static void LogDb(LogLevel level, string user, string message, string from, string ip = null)
        {
            var log = new DAL.Log();
            log.LogDt = DateTime.Now;
            log.Level = level;
            log.Message = message;
            log.Summary = message.Summary(256);
            log.From = from;

            // Operator：优先使用显式传的 user，否则兜底取当前登录用户名
            if (!string.IsNullOrWhiteSpace(user))
                log.Operator = user;
            else
                log.Operator = Auth.GetUserName(Asp.Current) ?? "";

            // 若传了显式 IP 直接用；否则尝试从请求上下文获取
            if (!string.IsNullOrWhiteSpace(ip))
                log.IP = ip;

            if (Asp.Request != null)
            {
                log.URL = Asp.Url;
                log.Method = Asp.Request.Method;
                log.Referrer = Asp.GetUrlReferrer();
                if (string.IsNullOrWhiteSpace(log.IP))
                    log.IP = Asp.ClientIP;
            }
            else
            {
                // 无请求上下文时再兜底（非 Web 请求的后台写日志，Asp.ClientIP 走 try/catch 已安全）
                if (string.IsNullOrWhiteSpace(log.IP) && Asp.IsWeb)
                {
                    try { log.IP = Asp.ClientIP; } catch { }
                }
            }
            log.Save();
        }

        /// <summary>保存到数据库（自动取当前登录用户名）</summary>
        public static void LogDb(LogLevel level, string message, string from)
            => LogDb(level, user: null, message: message, from: from);

        /// <summary>审计日志统一入口。显式传 operatorName/ip 的，会优先覆盖"自动取当前登录上下文"（例如登录成功/失败此时 User Claim 还没写入）</summary>
        public static void LogAudit(string module, string action, string message, string operatorName = null, string ip = null)
            => LogDb(LogLevel.Info, user: operatorName, message: message, from: $"{module}/{action}", ip: ip);

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
