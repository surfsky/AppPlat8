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

        /// <summary>保存到数据库</summary>
        /// <param name="level">等级</param>
        /// <param name="user">用户名</param>
        /// <param name="message">信息</param>
        /// <param name="from">来自那个客户端</param>
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
                //log.Operator = Asp.User?.Identity?.Name;
                log.URL = Asp.Url;
                log.IP = Asp.ClientIP;
                log.Method = Asp.Request.Method;
                log.Referrer = Asp.GetUrlReferrer();
            }
            log.Save();
        }
    }
}
