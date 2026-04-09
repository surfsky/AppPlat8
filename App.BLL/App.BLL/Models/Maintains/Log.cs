using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;
using Z.EntityFramework.Plus;

namespace App.DAL
{
    /// <summary>日志级别</summary>
    public enum LogLevel
    {
        [UI("调试")] Debug = 0,
        [UI("提示")] Info = 1,
        [UI("警告")] Warn = 2,
        [UI("错误")] Error = 3,
        [UI("致命")] Fatal = 4,
    }

    /// <summary>日志</summary>
    [UI("系统", "日志")]
    public class Log : EntityBase<Log>
    {
        [UI("记录时间")]                           public DateTime? LogDt { get; set; }
        [UI("级别")]                               public LogLevel? Level { get; set; }
        [UI("操作者")]                             public string Operator { get; set; }
        [UI("摘要")]                               public string Summary { get; set; }
        [UI("信息", Editor = EditorType.Html)]     public string Message { get; set; }
        [UI("访问URL")]                            public string URL { get; set; }
        [UI("访问方式")]                           public string Method { get; set; }
        [UI("前网页")]                             public string Referrer { get; set; }
        [UI("请求IP")]                             public string IP { get; set; }
        [UI("请求来自")]                           public string From { get; set; }

        [UI("级别")]                               public string LevelName { get { return Level.GetTitle(); } }

        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        // 查找
        public static IQueryable<Log> Search(string user, string msg, LogLevel? level=null, DateTime? fromDt = null, string ip = "", string from="")
        {
            IQueryable<Log> q = Set;
            if (user.IsNotEmpty())   q = q.Where(l => l.Operator.Contains(user));
            if (msg.IsNotEmpty())    q = q.Where(l => l.Message.Contains(msg));
            if (ip.IsNotEmpty())     q = q.Where(l => l.IP.Contains(ip));
            if (from.IsNotEmpty())   q = q.Where(l => l.From.Contains(from));
            if (level != null)       q = q.Where(l => l.Level == level);
            if (fromDt != null)      q = q.Where(t => t.LogDt >= fromDt);
            return q;
        }

        /// <summary>删除n个月前的数据</summary>
        public static int DeleteBatch(int months = 1)
        {
            var lastMonth = DateTime.Now.AddMonths(-months);
            int n = Set.Where(t => t.LogDt <= lastMonth).Delete();
            return n;
        }
    }
}